using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Server;

namespace FModel.MCP.Resources;

[McpServerResourceType]
public sealed class FModelResources(McpAssetService service)
{
    private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = true };

    [McpServerResource(UriTemplate = "fmodel://project", Name = "Current Project", MimeType = "application/json")]
    [Description("Information about the game project currently opened in FModel: game name, directory, UE version, mappings, archives and load status.")]
    public string Project()
        => JsonSerializer.Serialize(service.GetProjectInfo(), SerializerOptions);

    [McpServerResource(UriTemplate = "fmodel://archives", Name = "Game Archives", MimeType = "application/json")]
    [Description("The game archives (pak/utoc containers) of the current project with mount and encryption state.")]
    public string Archives()
        => JsonSerializer.Serialize(service.ListArchives(), SerializerOptions);

    [McpServerResource(UriTemplate = "fmodel://folder/{+path}", Name = "Folder Listing", MimeType = "application/json")]
    [Description("Sub-folders and files of a virtual folder in the loaded game archives.")]
    public string Folder(string path)
        => JsonSerializer.Serialize(service.ListFolder(path, true), SerializerOptions);

    [McpServerResource(UriTemplate = "fmodel://asset/{+path}", Name = "Asset JSON", MimeType = "application/json")]
    [Description("The JSON-serialized exports of a UE package (uasset/umap), truncated when very large.")]
    public string Asset(string path)
        => service.GetAssetJson(path, null, 100_000);
}
