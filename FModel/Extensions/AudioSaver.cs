using System.IO;
using CUE4Parse.Utils;
using FModel.Settings;
using FModel.ViewModels;

namespace FModel.Extensions;

/// <summary>
/// Headless audio file writer shared by the UI extract path and the MCP server.
/// </summary>
public static class AudioSaver
{
    public static string ResolveOutputPath(string fullPath, string ext)
    {
        if (fullPath.StartsWith('/')) fullPath = fullPath[1..];
        var baseFilePath = UserSettings.Default.KeepDirectoryStructure ? fullPath : fullPath.SubstringAfterLast('/');
        var combinedPath = Path.Combine(UserSettings.Default.AudioDirectory, baseFilePath);
        return Path.ChangeExtension(combinedPath, ext.ToLowerInvariant()).Replace('\\', '/');
    }

    public static bool TrySave(string fullPath, string ext, byte[] data, out string savedAudioPath)
    {
        var extLower = ext.ToLowerInvariant();
        savedAudioPath = ResolveOutputPath(fullPath, extLower);
        Directory.CreateDirectory(Path.GetDirectoryName(savedAudioPath));

        if (UserSettings.Default.ConvertAudioOnBulkExport && extLower is not "wav")
        {
            if (!AudioPlayerViewModel.TryConvert(savedAudioPath, data, extLower, out var wavFilePath))
                return false;

            savedAudioPath = wavFilePath;
            return true;
        }

        using var stream = new FileStream(savedAudioPath, FileMode.Create, FileAccess.Write);
        stream.Write(data);
        return true;
    }
}
