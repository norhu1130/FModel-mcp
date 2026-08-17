using System.ComponentModel;
using System.Threading.Tasks;
using ModelContextProtocol.Server;

namespace FModel.MCP.Tools;

[McpServerToolType]
public sealed class ExportTools(McpAssetService service)
{
    [McpServerTool(Name = "export_raw")]
    [Description("Save the raw bytes of a file (all package parts: uasset/uexp/ubulk) to FModel's Raw Data output directory. Returns the written file paths.")]
    public McpSavedFiles ExportRaw(
        [Description("Virtual file path to export.")] string path)
        => service.ExportRaw(path);

    [McpServerTool(Name = "save_properties_json")]
    [Description("Serialize a UE package to JSON and save the full document to FModel's Properties output directory. Returns the written file path.")]
    public McpSavedFiles SavePropertiesJson(
        [Description("Virtual file path of a uasset/umap.")] string path)
        => service.SavePropertiesJson(path);

    [McpServerTool(Name = "save_texture")]
    [Description("Export the textures of a UE package to image files (format follows the user's FModel settings). Returns the written file paths. Fails with BUSY while another export is running.")]
    public Task<McpExportOutcome> SaveTexture(
        [Description("Virtual file path of a uasset containing textures.")] string path)
        => service.ExportObjectsAsync(path, EBulkType.Textures);

    [McpServerTool(Name = "export_model")]
    [Description("Export the static/skeletal meshes of a UE package to model files (format follows the user's FModel settings). Returns the written file paths. Fails with BUSY while another export is running.")]
    public Task<McpExportOutcome> ExportModel(
        [Description("Virtual file path of a uasset containing meshes.")] string path)
        => service.ExportObjectsAsync(path, EBulkType.Meshes);

    [McpServerTool(Name = "export_animation")]
    [Description("Export the animations of a UE package to animation files (format follows the user's FModel settings). Returns the written file paths. Fails with BUSY while another export is running.")]
    public Task<McpExportOutcome> ExportAnimation(
        [Description("Virtual file path of a uasset containing animations.")] string path)
        => service.ExportObjectsAsync(path, EBulkType.Animations);

    [McpServerTool(Name = "export_world")]
    [Description("Export the world/level of a umap package (format follows the user's FModel settings). Worlds with streaming levels may pop a filter dialog in the FModel UI. Fails with BUSY while another export is running.")]
    public Task<McpExportOutcome> ExportWorld(
        [Description("Virtual file path of a umap.")] string path)
        => service.ExportObjectsAsync(path, EBulkType.Worlds);

    [McpServerTool(Name = "export_audio")]
    [Description("Extract and save the audio of a package (Wwise/FMOD banks and events, sound waves) or of a raw audio container file to FModel's Audio output directory. Returns the written file paths.")]
    public McpSavedFiles ExportAudio(
        [Description("Virtual file path of an audio asset or container.")] string path)
        => service.ExportAudio(path);
}
