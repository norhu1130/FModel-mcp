using System.ComponentModel;
using ModelContextProtocol.Server;

namespace FModel.MCP.Prompts;

[McpServerPromptType]
public sealed class FModelPrompts
{
    [McpServerPrompt(Name = "analyze_asset")]
    [Description("Analyze a game asset: type, purpose, key properties and what references it.")]
    public static string AnalyzeAsset(
        [Description("Virtual file path of the asset to analyze, e.g. 'GameName/Content/.../Asset.uasset'.")] string path)
        => $"Analyze the game asset at '{path}' using the FModel tools:\n" +
           $"1. Call get_asset_metadata to understand the package structure (classes, imports, exports).\n" +
           $"2. Call get_asset_json to read the exports; if truncated, re-query per export with objectName.\n" +
           $"3. Call get_references to find which packages reference it.\n" +
           $"Then summarize: the asset's type and purpose, its key properties and values, and how it is used by the packages that reference it.";

    [McpServerPrompt(Name = "find_assets")]
    [Description("Find and summarize game assets related to a topic.")]
    public static string FindAssets(
        [Description("What to look for, e.g. a character, weapon or system name.")] string topic,
        [Description("Folder to start from (optional), e.g. 'GameName/Content/Characters'.")] string folderHint = null)
        => $"Find assets related to '{topic}' in the loaded game:\n" +
           $"1. Call get_project_info to know which game is loaded.\n" +
           $"2. {(string.IsNullOrEmpty(folderHint) ? "Call list_folder on the root to understand the content layout." : $"Call list_folder on '{folderHint}' and explore from there.")}\n" +
           $"3. Call search_files with several substring variants of '{topic}' (and a regex variant if useful), filtering by extension where it helps.\n" +
           $"4. For the most promising matches, call get_asset_json to inspect them.\n" +
           $"Present the best matches grouped by folder, with a one-line description of what each asset is.";
}
