using System.ComponentModel;
using ModelContextProtocol.Server;

namespace FModel.MCP.Tools;

[McpServerToolType]
public sealed class BrowseTools(McpAssetService service)
{
    [McpServerTool(Name = "list_folder")]
    [Description("List the sub-folders and files of a virtual folder in the loaded game archives. Use an empty path for the root. File listings are capped at 1000 entries per folder; 'fileCount' always reports the real total.")]
    public McpFolderListing ListFolder(
        [Description("Virtual folder path, e.g. 'GameName/Content/Characters'. Empty or omitted lists the root.")] string path = "",
        [Description("Whether to include the folder's files in the response (default true).")] bool includeFiles = true)
        => service.ListFolder(path, includeFiles);

    [McpServerTool(Name = "search_files")]
    [Description("Search file paths across all loaded archives. By default every whitespace-separated term must appear in the path (case-insensitive); set isRegex for a regular expression match. Returns paths usable with the asset and export tools.")]
    public McpSearchResult SearchFiles(
        [Description("Search terms (space-separated, all must match) or a regular expression when isRegex is true.")] string query,
        [Description("Treat the query as a .NET regular expression (default false).")] bool isRegex = false,
        [Description("Only return files with this extension, e.g. 'uasset' (optional).")] string extension = null,
        [Description("Maximum results per page, 1-1000 (default 100).")] int limit = 100,
        [Description("Number of matches to skip, for pagination (default 0).")] int offset = 0)
        => service.SearchFiles(query, isRegex, extension, limit, offset);
}
