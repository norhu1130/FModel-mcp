using System.Collections.Generic;
using System.ComponentModel;
using ModelContextProtocol.Server;

namespace FModel.MCP.Tools;

[McpServerToolType]
public sealed class ProjectTools(McpAssetService service)
{
    [McpServerTool(Name = "get_project_info")]
    [Description("Get information about the game project currently opened in FModel: game name, directory, Unreal Engine version, mappings, archive counts, loaded file count and output directories. Call this first to understand what is loaded.")]
    public McpProjectInfo GetProjectInfo() => service.GetProjectInfo();

    [McpServerTool(Name = "list_archives")]
    [Description("List the game archives (pak/utoc containers) of the current project, including whether each one is mounted, encrypted, its mount point and file count.")]
    public IReadOnlyList<McpArchiveInfo> ListArchives() => service.ListArchives();
}
