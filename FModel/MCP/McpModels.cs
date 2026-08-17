using System.Collections.Generic;

namespace FModel.MCP;

// Plain POCOs serialized by the MCP SDK (System.Text.Json). FModel-internal asset JSON is
// produced with Newtonsoft.Json and transported as raw strings, never through these DTOs.

public sealed record McpProjectInfo(
    string ProjectName,
    string GameDisplayName,
    string GameDirectory,
    string UeVersion,
    string TexturePlatform,
    string MappingsProvider,
    bool IsLoaded,
    int LoadedFileCount,
    int MountedArchiveCount,
    int UnloadedArchiveCount,
    McpOutputDirectories OutputDirectories);

public sealed record McpOutputDirectories(
    string Output,
    string RawData,
    string Properties,
    string Textures,
    string Audio,
    string Code,
    string Models);

public sealed record McpArchiveInfo(
    string Name,
    bool IsMounted,
    bool IsEncrypted,
    string EncryptionKeyGuid,
    string MountPoint,
    int FileCount,
    long Length);

public sealed record McpFileEntry(string Name, long Size, string Extension);

public sealed record McpFolderListing(
    string Path,
    IReadOnlyList<string> Folders,
    IReadOnlyList<McpFileEntry> Files,
    int FileCount,
    int FolderCount);

public sealed record McpSearchResult(
    int Total,
    IReadOnlyList<string> Items,
    int? NextOffset);

public sealed record McpSavedFiles(IReadOnlyList<string> FilePaths);

public sealed record McpExportOutcome(
    int Succeeded,
    int Failed,
    IReadOnlyList<string> FilePaths,
    IReadOnlyList<string> Errors);
