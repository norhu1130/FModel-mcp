using System.Collections.Generic;
using System.ComponentModel;
using ModelContextProtocol.Server;

namespace FModel.MCP.Tools;

[McpServerToolType]
public sealed class AssetTools(McpAssetService service)
{
    [McpServerTool(Name = "get_asset_json")]
    [Description("Deserialize a UE package (uasset/umap) and return its exports as JSON. Known text formats (json, ini, ...) are returned as-is. Large results are truncated inline and the full JSON is saved to the Properties output directory; narrow with objectName to read a single export.")]
    public string GetAssetJson(
        [Description("Virtual file path, e.g. 'GameName/Content/.../Asset.uasset'.")] string path,
        [Description("Name of a single export to return instead of the whole package (optional).")] string objectName = null,
        [Description("Maximum characters to return inline before truncating (default 100000).")] int maxChars = 100_000)
        => service.GetAssetJson(path, objectName, maxChars);

    [McpServerTool(Name = "get_asset_metadata")]
    [Description("Return the package-level metadata of a UE package as JSON: summary, name map and import/export tables. Lighter than get_asset_json for understanding a package's structure.")]
    public string GetAssetMetadata(
        [Description("Virtual file path of a uasset/umap.")] string path,
        [Description("Maximum characters to return inline before truncating (default 100000).")] int maxChars = 100_000)
        => service.GetAssetMetadata(path, maxChars);

    [McpServerTool(Name = "get_references")]
    [Description("Scan all mounted archives for packages that reference the given package and return their paths. This can take a while on large games.")]
    public IReadOnlyList<string> GetReferences(
        [Description("Virtual file path of the package to find references to.")] string path)
        => service.GetReferences(path);

    [McpServerTool(Name = "decompile_blueprint")]
    [Description("Decompile the blueprint classes of a UE package into pseudo C++. Large results are truncated inline and the full text is saved to the Code output directory.")]
    public string DecompileBlueprint(
        [Description("Virtual file path of a uasset containing blueprint classes.")] string path,
        [Description("Maximum characters to return inline before truncating (default 100000).")] int maxChars = 100_000)
        => service.DecompileBlueprint(path, maxChars);
}
