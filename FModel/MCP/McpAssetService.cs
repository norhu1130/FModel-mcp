using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using CUE4Parse.FileProvider.Objects;
using CUE4Parse.FileProvider.Vfs;
using CUE4Parse.UE4.Assets;
using CUE4Parse.UE4.Assets.Exports.Animation;
using CUE4Parse.UE4.Assets.Exports.Fmod;
using CUE4Parse.UE4.Assets.Exports.SkeletalMesh;
using CUE4Parse.UE4.Assets.Exports.Sound;
using CUE4Parse.UE4.Assets.Exports.StaticMesh;
using CUE4Parse.UE4.Assets.Exports.Texture;
using CUE4Parse.UE4.Assets.Exports.Wwise;
using CUE4Parse.UE4.Objects.Engine;
using CUE4Parse.UE4.Objects.UObject;
using CUE4Parse.UE4.VirtualFileSystem;
using CUE4Parse.Utils;
using CUE4Parse_Conversion.Sounds;
using FModel.Extensions;
using FModel.Settings;
using FModel.ViewModels;
using Newtonsoft.Json;

namespace FModel.MCP;

/// <summary>
/// Headless service layer between the MCP tools/resources and the CUE4Parse provider.
/// Reads go straight to <see cref="AbstractVfsFileProvider"/> and never touch UI view models,
/// so they are safe to run concurrently on Kestrel threads.
/// </summary>
public sealed class McpAssetService
{
    private const int MaxFilesPerFolderListing = 1000;
    private static readonly TimeSpan RegexTimeout = TimeSpan.FromSeconds(2);
    private static readonly HashSet<string> TextExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        "json", "json5", "ini", "txt", "xml", "html", "csv", "md", "log", "cfg", "yml", "yaml",
        "uproject", "uplugin", "upluginmanifest", "uefnproject", "verse", "lua", "manifest", "archive"
    };
    private static readonly HashSet<string> AudioFileExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        "wem", "bnk", "pck", "awb", "acb", "wav", "ogg", "mp3", "at9", "adpcm", "binka", "opus"
    };

    private readonly ApplicationViewModel _applicationView;
    private readonly SemaphoreSlim _exportGate = new(1, 1);

    private AbstractVfsFileProvider Provider => _applicationView.CUE4Parse.Provider;

    public McpAssetService(ApplicationViewModel applicationView)
    {
        _applicationView = applicationView;
        applicationView.CUE4Parse.Provider.VfsMounted += (_, _) => InvalidateFolderIndex();
        applicationView.CUE4Parse.Provider.VfsUnmounted += (_, _) => InvalidateFolderIndex();
    }

    #region readiness / lookup

    private void EnsureLoaded()
    {
        if (Provider.Files.Count == 0)
            throw McpErrors.NotLoaded();
    }

    private GameFile GetEntry(string path)
    {
        EnsureLoaded();
        if (string.IsNullOrWhiteSpace(path))
            throw McpErrors.Failed("a non-empty 'path' argument is required");

        path = path.Trim().Replace('\\', '/').TrimStart('/');
        if (Provider.Files.TryGetValue(path, out var file)) return file;

        try
        {
            if (Provider.Files.TryGetValue(Provider.FixPath(path), out file)) return file;
        }
        catch
        {
            // FixPath can throw on malformed input - fall through to the not-found error
        }

        throw McpErrors.NotFound(path, McpErrors.SuggestPaths(Provider.Files.Keys, path));
    }

    #endregion

    #region project / archives

    public McpProjectInfo GetProjectInfo()
    {
        var currentDir = UserSettings.Default.CurrentDir;
        var isLoaded = Provider.Files.Count > 0;

        string projectName = null, gameDisplayName = null;
        try
        {
            projectName = Provider.ProjectName;
            gameDisplayName = Provider.GameDisplayName;
        }
        catch
        {
            // both getters depend on mounted archives - nothing loaded yet
        }

        return new McpProjectInfo(
            string.IsNullOrEmpty(projectName) ? currentDir.GameName : projectName,
            gameDisplayName ?? currentDir.GameName,
            currentDir.GameDirectory,
            currentDir.UeVersion.ToString(),
            currentDir.TexturePlatform.ToString(),
            Provider.MappingsContainer?.GetType().Name ?? "none",
            isLoaded,
            Provider.Files.Count,
            Provider.MountedVfs.Count,
            Provider.UnloadedVfs.Count,
            new McpOutputDirectories(
                UserSettings.Default.OutputDirectory,
                UserSettings.Default.RawDataDirectory,
                UserSettings.Default.PropertiesDirectory,
                UserSettings.Default.TextureDirectory,
                UserSettings.Default.AudioDirectory,
                UserSettings.Default.CodeDirectory,
                UserSettings.Default.ModelDirectory));
    }

    public IReadOnlyList<McpArchiveInfo> ListArchives()
    {
        var archives = new List<McpArchiveInfo>();
        foreach (var reader in Provider.MountedVfs)
            archives.Add(DescribeArchive(reader, true));
        foreach (var reader in Provider.UnloadedVfs)
            archives.Add(DescribeArchive(reader, false));
        return archives.OrderBy(a => a.Name, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static McpArchiveInfo DescribeArchive(IAesVfsReader reader, bool mounted)
    {
        string mountPoint = string.Empty;
        var fileCount = 0;
        try
        {
            if (mounted)
            {
                mountPoint = reader.MountPoint;
                fileCount = reader.FileCount;
            }
        }
        catch
        {
            // some readers throw before being fully mounted - basic info is enough
        }

        return new McpArchiveInfo(reader.Name, mounted, reader.IsEncrypted,
            reader.EncryptionKeyGuid.ToString(), mountPoint, fileCount, reader.Length);
    }

    #endregion

    #region folder index / browse / search

    private sealed class FolderNode
    {
        public readonly SortedSet<string> Folders = new(StringComparer.OrdinalIgnoreCase);
        public readonly List<McpFileEntry> Files = [];
    }

    private Dictionary<string, FolderNode> _folderIndex;
    private int _indexedFileCount = -1;
    private readonly object _indexLock = new();

    private void InvalidateFolderIndex()
    {
        lock (_indexLock)
        {
            _folderIndex = null;
            _indexedFileCount = -1;
        }
    }

    private Dictionary<string, FolderNode> GetFolderIndex()
    {
        EnsureLoaded();
        var files = Provider.Files;
        var index = _folderIndex;
        if (index != null && _indexedFileCount == files.Count) return index;

        lock (_indexLock)
        {
            if (_folderIndex != null && _indexedFileCount == files.Count) return _folderIndex;

            var map = new Dictionary<string, FolderNode>(StringComparer.OrdinalIgnoreCase) { [string.Empty] = new() };
            foreach (var file in files.Values)
            {
                if (file.IsUePackagePayload) continue;

                var path = file.Path;
                var slash = path.LastIndexOf('/');
                var folder = slash < 0 ? string.Empty : path[..slash];

                if (!map.TryGetValue(folder, out var node)) map[folder] = node = new FolderNode();
                node.Files.Add(new McpFileEntry(slash < 0 ? path : path[(slash + 1)..], file.Size, file.Extension));

                // register the folder chain upwards; stop as soon as a link already exists
                var current = folder;
                while (current.Length > 0)
                {
                    var parentSlash = current.LastIndexOf('/');
                    var parent = parentSlash < 0 ? string.Empty : current[..parentSlash];
                    var name = parentSlash < 0 ? current : current[(parentSlash + 1)..];

                    if (!map.TryGetValue(parent, out var parentNode)) map[parent] = parentNode = new FolderNode();
                    if (!parentNode.Folders.Add(name)) break;
                    current = parent;
                }
            }

            _indexedFileCount = files.Count;
            _folderIndex = map;
            return map;
        }
    }

    public McpFolderListing ListFolder(string path, bool includeFiles)
    {
        var index = GetFolderIndex();
        var key = (path ?? string.Empty).Trim().Replace('\\', '/').Trim('/');
        if (!index.TryGetValue(key, out var node))
        {
            var lastSegment = key.SubstringAfterLast('/');
            var suggestions = index.Keys
                .Where(k => k.Length > 0 && k.SubstringAfterLast('/').Equals(lastSegment, StringComparison.OrdinalIgnoreCase))
                .Take(3);
            throw McpErrors.NotFound(key.Length == 0 ? "<root>" : key, suggestions);
        }

        var folders = node.Folders.Select(name => key.Length == 0 ? name : $"{key}/{name}").ToArray();
        McpFileEntry[] fileEntries = includeFiles
            ? node.Files.OrderBy(f => f.Name, StringComparer.OrdinalIgnoreCase).Take(MaxFilesPerFolderListing).ToArray()
            : [];

        return new McpFolderListing(key, folders, fileEntries, node.Files.Count, folders.Length);
    }

    public McpSearchResult SearchFiles(string query, bool isRegex, string extension, int limit, int offset)
    {
        EnsureLoaded();
        if (string.IsNullOrWhiteSpace(query))
            throw McpErrors.Failed("a non-empty 'query' argument is required");

        limit = Math.Clamp(limit, 1, 1000);
        offset = Math.Max(0, offset);
        extension = extension?.TrimStart('.');

        Regex regex = null;
        string[] terms = null;
        if (isRegex)
        {
            try
            {
                regex = new Regex(query, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, RegexTimeout);
            }
            catch (ArgumentException e)
            {
                throw McpErrors.Failed($"invalid regex '{query}'", e);
            }
        }
        else
        {
            terms = query.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        }

        var total = 0;
        var items = new List<string>(limit);
        try
        {
            foreach (var file in Provider.Files.Values)
            {
                if (file.IsUePackagePayload) continue;
                if (extension != null && !file.Extension.Equals(extension, StringComparison.OrdinalIgnoreCase)) continue;

                var path = file.Path;
                var match = regex != null
                    ? regex.IsMatch(path)
                    : terms.All(term => path.Contains(term, StringComparison.OrdinalIgnoreCase));
                if (!match) continue;

                if (total >= offset && items.Count < limit) items.Add(path);
                total++;
            }
        }
        catch (RegexMatchTimeoutException)
        {
            throw McpErrors.Failed($"regex '{query}' timed out after {RegexTimeout.TotalSeconds:0}s - simplify the pattern");
        }

        var nextOffset = offset + items.Count;
        return new McpSearchResult(total, items, nextOffset < total ? nextOffset : null);
    }

    #endregion

    #region asset reads

    public string GetAssetJson(string path, string objectName, int maxChars)
    {
        var entry = GetEntry(path);
        maxChars = NormalizeMaxChars(maxChars);

        if (!entry.IsUePackage)
        {
            if (!TextExtensions.Contains(entry.Extension))
                throw McpErrors.Unsupported($"'{entry.Path}' is not a UE package or a known text format. Use export_raw to dump its bytes to disk.");

            var text = Encoding.UTF8.GetString(Provider.SaveAsset(entry));
            return TruncateInline(text, maxChars, null, null);
        }

        var pkg = Provider.LoadPackage(entry);
        object displayData;
        if (!string.IsNullOrEmpty(objectName))
        {
            displayData = pkg.GetExportOrNull(objectName, StringComparison.OrdinalIgnoreCase)
                          ?? throw McpErrors.Failed($"no export named '{objectName}' in '{entry.Path}'. Exports: {string.Join(", ", GetExportNames(pkg).Take(50))}");
        }
        else
        {
            displayData = pkg.GetExports();
        }

        var json = JsonConvert.SerializeObject(displayData, Formatting.Indented);
        if (json.Length <= maxChars) return json;

        var filePath = WriteTextFile(UserSettings.Default.PropertiesDirectory, entry, ".json", json);
        return TruncateInline(json, maxChars, filePath, GetExportNames(pkg));
    }

    public string GetAssetMetadata(string path, int maxChars)
    {
        var entry = GetEntry(path);
        if (!entry.IsUePackage)
            throw McpErrors.Unsupported($"'{entry.Path}' is not a UE package - metadata is only available for uasset/umap files.");

        var json = JsonConvert.SerializeObject(Provider.LoadPackage(entry), Formatting.Indented);
        return TruncateInline(json, NormalizeMaxChars(maxChars), null, null);
    }

    public IReadOnlyList<string> GetReferences(string path)
    {
        var entry = GetEntry(path);
        return Provider.ScanForPackageRefs(entry).Select(f => f.Path).ToArray();
    }

    public string DecompileBlueprint(string path, int maxChars)
    {
        var entry = GetEntry(path);
        if (!entry.IsUePackage)
            throw McpErrors.Unsupported($"'{entry.Path}' is not a UE package.");

        if (!BlueprintDecompiler.TryDecompile(Provider, entry, out var cpp))
            throw McpErrors.Unsupported($"'{entry.Path}' contains no blueprint classes to decompile.");

        maxChars = NormalizeMaxChars(maxChars);
        if (cpp.Length <= maxChars) return cpp;

        var filePath = WriteTextFile(UserSettings.Default.CodeDirectory, entry, ".cpp", cpp);
        return TruncateInline(cpp, maxChars, filePath, null);
    }

    private static List<string> GetExportNames(IPackage pkg)
    {
        var names = new List<string>(Math.Min(pkg.ExportMapLength, 200));
        for (var i = 0; i < pkg.ExportMapLength && names.Count < 200; i++)
        {
            var pointer = new FPackageIndex(pkg, i + 1).ResolvedObject;
            if (pointer != null) names.Add(pointer.Name.Text);
        }

        return names;
    }

    private static int NormalizeMaxChars(int maxChars) => maxChars <= 0 ? 100_000 : Math.Max(1_000, maxChars);

    private static string TruncateInline(string content, int maxChars, string filePath, IReadOnlyList<string> exportNames)
    {
        if (content.Length <= maxChars) return content;

        var header = new StringBuilder()
            .Append($"[TRUNCATED: showing the first {maxChars:N0} of {content.Length:N0} characters.");
        if (filePath != null) header.Append($" The full content has been saved to \"{filePath}\".");
        if (exportNames is { Count: > 0 })
        {
            header.Append($" Re-query with 'objectName' to narrow to one export - exports: {string.Join(", ", exportNames.Take(50))}");
            if (exportNames.Count > 50) header.Append(", ...");
            header.Append('.');
        }

        header.Append("]\n");
        return header.Append(content, 0, maxChars).ToString();
    }

    private static string WriteTextFile(string baseDirectory, GameFile entry, string extension, string content)
    {
        var directory = Path.Combine(baseDirectory, UserSettings.Default.KeepDirectoryStructure ? entry.Directory : string.Empty).Replace('\\', '/');
        Directory.CreateDirectory(directory);

        var filePath = Path.Combine(directory, entry.NameWithoutExtension + extension).Replace('\\', '/');
        File.WriteAllText(filePath, content);
        return filePath;
    }

    #endregion

    #region exports

    public McpSavedFiles ExportRaw(string path)
    {
        var entry = GetEntry(path);

        IReadOnlyDictionary<string, byte[]> parts;
        try
        {
            parts = Provider.SavePackage(entry);
        }
        catch (Exception e)
        {
            throw McpErrors.Failed($"could not read '{entry.Path}'", e);
        }

        var saved = new List<string>(parts.Count);
        foreach (var (partPath, bytes) in parts)
        {
            var relative = UserSettings.Default.KeepDirectoryStructure ? partPath.TrimStart('/') : partPath.SubstringAfterLast('/');
            var filePath = Path.Combine(UserSettings.Default.RawDataDirectory, relative).Replace('\\', '/');
            Directory.CreateDirectory(filePath.SubstringBeforeLast('/'));
            File.WriteAllBytes(filePath, bytes);
            saved.Add(filePath);
        }

        return new McpSavedFiles(saved);
    }

    public McpSavedFiles SavePropertiesJson(string path)
    {
        var entry = GetEntry(path);
        if (!entry.IsUePackage)
            throw McpErrors.Unsupported($"'{entry.Path}' is not a UE package. Use export_raw instead.");

        var json = JsonConvert.SerializeObject(Provider.LoadPackage(entry).GetExports(), Formatting.Indented);
        var filePath = WriteTextFile(UserSettings.Default.PropertiesDirectory, entry, ".json", json);
        return new McpSavedFiles([filePath]);
    }

    public async Task<McpExportOutcome> ExportObjectsAsync(string path, EBulkType kind)
    {
        var entry = GetEntry(path);
        if (!entry.IsUePackage)
            throw McpErrors.Unsupported($"'{entry.Path}' is not a UE package.");

        if (!_exportGate.Wait(0))
            throw McpErrors.Busy("another MCP export is already running.");

        try
        {
            var sessionVm = ExportSessionViewModel.Instance;
            if (sessionVm.IsRunning || sessionVm.Session.TotalQueued > 0)
                throw McpErrors.Busy("an export is already queued or running in the FModel UI.");

            var queued = 0;
            try
            {
                var pkg = Provider.LoadPackage(entry);
                for (var i = 0; i < pkg.ExportMapLength; i++)
                {
                    try
                    {
                        var pointer = new FPackageIndex(pkg, i + 1).ResolvedObject;
                        if (pointer?.Object is null) continue;

                        var dummy = ((AbstractUePackage) pkg).ConstructObject(pointer.Class, pkg);
                        var match = kind switch
                        {
                            EBulkType.Textures => dummy is UTexture,
                            EBulkType.Meshes => dummy is UStaticMesh or USkeletalMesh || (dummy is USkeleton && UserSettings.Default.SaveSkeletonAsMesh),
                            EBulkType.Animations => dummy is UAnimationAsset,
                            EBulkType.Worlds => dummy is UWorld,
                            _ => false
                        };
                        if (!match) continue;

                        sessionVm.Session.Add(pointer.Object.Value);
                        queued++;
                    }
                    catch
                    {
                        // skip exports that fail to resolve, same as the UI bulk path
                    }
                }

                if (queued == 0)
                    throw McpErrors.Unsupported($"no exportable {kind} objects found in '{entry.Path}'.");
            }
            catch
            {
                if (queued > 0) sessionVm.Session.Clear();
                throw;
            }

            var results = await sessionVm.ExportAsync().ConfigureAwait(false);
            if (results is null)
                throw McpErrors.Failed("the export session did not run.");

            var filePaths = results.Where(r => r.Success).SelectMany(r => r.DiskFilePaths ?? []).ToArray();
            var errors = results.Where(r => !r.Success)
                .Select(r => $"{r.ObjectPath}: {r.Error?.GetBaseException().Message ?? "unknown error"}")
                .ToArray();
            return new McpExportOutcome(results.Count(r => r.Success), errors.Length, filePaths, errors);
        }
        finally
        {
            _exportGate.Release();
        }
    }

    public McpSavedFiles ExportAudio(string path)
    {
        var entry = GetEntry(path);
        var saved = new List<string>();

        if (!entry.IsUePackage)
        {
            if (!AudioFileExtensions.Contains(entry.Extension))
                throw McpErrors.Unsupported($"'{entry.Path}' is neither a UE package nor a known audio container. Use export_raw to dump its bytes.");

            if (!AudioSaver.TrySave(entry.PathWithoutExtension, entry.Extension, entry.Read(), out var savedPath))
                throw McpErrors.Failed($"could not save or convert '{entry.Path}'");

            return new McpSavedFiles([savedPath]);
        }

        var cue4Parse = _applicationView.CUE4Parse;
        var shouldDecompress = UserSettings.Default.CompressedAudioMode == ECompressedAudio.PlayDecompressed;
        var pkg = Provider.LoadPackage(entry);
        for (var i = 0; i < pkg.ExportMapLength; i++)
        {
            try
            {
                var pointer = new FPackageIndex(pkg, i + 1).ResolvedObject;
                if (pointer?.Object is null) continue;

                var dummy = ((AbstractUePackage) pkg).ConstructObject(pointer.Class, pkg);
                switch (dummy)
                {
                    case UAkAudioBank when pointer.Object.Value is UAkAudioBank bank:
                    {
                        foreach (var sound in cue4Parse.WwiseProvider.ExtractBankSounds(bank))
                            SaveSound(sound.OutputPath, sound.Extension, sound.Data?.GetData() ?? [], saved);
                        break;
                    }
                    case UAkAudioEvent when pointer.Object.Value is UAkAudioEvent audioEvent:
                    {
                        foreach (var sound in cue4Parse.WwiseProvider.ExtractAudioEventSounds(audioEvent))
                            SaveSound(sound.OutputPath, sound.Extension, sound.Data?.GetData() ?? [], saved);
                        break;
                    }
                    case UFMODEvent when pointer.Object.Value is UFMODEvent fmodEvent:
                    {
                        var directory = Path.GetDirectoryName(Provider.FixPath(fmodEvent.Owner?.Name ?? "/FMOD/Desktop/"));
                        foreach (var sound in cue4Parse.FmodProvider.ExtractEventSounds(fmodEvent))
                            SaveSound(Path.Combine(directory, sound.Name).Replace('\\', '/'), sound.Extension, sound.Data, saved);
                        break;
                    }
                    case UFMODBank when pointer.Object.Value is UFMODBank fmodBank:
                    {
                        var directory = Path.GetDirectoryName(Provider.FixPath(fmodBank.Owner?.Name ?? "/FMOD/Desktop/"));
                        foreach (var sound in cue4Parse.FmodProvider.ExtractBankSounds(fmodBank))
                            SaveSound(Path.Combine(directory, sound.Name).Replace('\\', '/'), sound.Extension, sound.Data, saved);
                        break;
                    }
                    case UAkMediaAsset when pointer.Object.Value is UAkMediaAsset akMediaAsset:
                    {
                        var audioName = akMediaAsset.MediaName ?? akMediaAsset.Name;
                        var outputPath = Path.Combine(entry.PathWithoutExtension.Replace('\\', '/').SubstringBeforeLast('/'), audioName);
                        if (akMediaAsset.CurrentMediaAssetData?.ResolvedObject?.Object?.Value is UAkMediaAssetData akMediaAssetData)
                        {
                            akMediaAssetData.Decode(shouldDecompress, out var audioFormat, out var data);
                            if (data != null && !string.IsNullOrEmpty(audioFormat))
                                SaveSound(outputPath, audioFormat, data, saved);
                        }

                        break;
                    }
                    case UAkMediaAssetData:
                    case USoundWave:
                    {
                        if (pointer.Object.Value is UAkMediaAssetData dataObj && dataObj.Outer.Object.Value is UAkMediaAsset)
                            break; // handled through the owning UAkMediaAsset

                        pointer.Object.Value.Decode(shouldDecompress, out var audioFormat, out var data);
                        if (data != null && !string.IsNullOrEmpty(audioFormat))
                            SaveSound(entry.PathWithoutExtension, audioFormat, data, saved);
                        break;
                    }
                }
            }
            catch
            {
                // skip exports that fail to resolve or decode, same as the UI bulk path
            }
        }

        if (saved.Count == 0)
            throw McpErrors.Unsupported($"no supported audio exports found in '{entry.Path}'. Game-specific audio formats may only be exportable through the FModel UI; export_raw is always available.");

        return new McpSavedFiles(saved);
    }

    private static void SaveSound(string fullPath, string ext, byte[] data, List<string> saved)
    {
        if (data is not { Length: > 0 }) return;
        if (AudioSaver.TrySave(fullPath, ext, data, out var savedPath))
            saved.Add(savedPath);
    }

    #endregion
}
