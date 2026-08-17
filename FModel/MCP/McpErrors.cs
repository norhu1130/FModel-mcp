using System;
using System.Collections.Generic;
using System.Linq;
using CUE4Parse.Utils;
using ModelContextProtocol;

namespace FModel.MCP;

/// <summary>
/// Uniform error surface for MCP tools. Everything is thrown as <see cref="McpException"/>
/// so the SDK reports proper JSON-RPC tool errors instead of opaque 500s.
/// </summary>
public static class McpErrors
{
    public static McpException NotLoaded() =>
        new("NOT_LOADED: no files are loaded yet - the game is still mounting, or no working AES keys have been submitted. Ask the user to load a game in FModel, then retry.");

    public static McpException NotFound(string path, IEnumerable<string> suggestions)
    {
        var didYouMean = suggestions.ToArray();
        var extra = didYouMean.Length > 0
            ? $" Did you mean: {string.Join(", ", didYouMean)}"
            : " Use search_files or list_folder to locate the correct path.";
        return new McpException($"NOT_FOUND: '{path}' does not exist in the loaded archives.{extra}");
    }

    public static McpException Busy(string reason) =>
        new($"BUSY: {reason} Retry once the current operation is finished.");

    public static McpException Unsupported(string message) =>
        new($"UNSUPPORTED: {message}");

    public static McpException Failed(string message, Exception e = null) =>
        new($"FAILED: {message}{(e is null ? "" : $" ({e.GetBaseException().Message})")}");

    /// <summary>cheap suffix search over the provider keys for "did you mean" hints</summary>
    public static IEnumerable<string> SuggestPaths(IEnumerable<string> keys, string path, int max = 3)
    {
        var fileName = path.Replace('\\', '/').SubstringAfterLast('/');
        if (string.IsNullOrWhiteSpace(fileName)) yield break;

        var count = 0;
        foreach (var key in keys)
        {
            if (!key.SubstringAfterLast('/').Contains(fileName, StringComparison.OrdinalIgnoreCase))
                continue;

            yield return key;
            if (++count >= max) yield break;
        }
    }
}
