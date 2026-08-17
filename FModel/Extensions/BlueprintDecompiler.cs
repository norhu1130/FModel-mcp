using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using CUE4Parse.FileProvider;
using CUE4Parse.FileProvider.Objects;
using CUE4Parse.UE4.Assets;
using CUE4Parse.UE4.Objects.UObject;
using CUE4Parse.UE4.Objects.UObject.Editor;

namespace FModel.Extensions;

/// <summary>
/// Headless blueprint decompilation shared by the UI extract path and the MCP server.
/// </summary>
public static class BlueprintDecompiler
{
    public static bool TryDecompile(IFileProvider provider, GameFile entry, out string pseudoCpp)
    {
        pseudoCpp = null;

        UClassCookedMetaData cookedMetaData = null;
        try
        {
            var editorPkg = provider.LoadPackage(entry.Path.Replace(".uasset", ".o.uasset"));
            cookedMetaData = editorPkg.GetExport<UClassCookedMetaData>("CookedClassMetaData");
        }
        catch
        {
            // ignored
        }

        var cppList = new List<string>();
        var pkg = provider.LoadPackage(entry);
        for (var i = 0; i < pkg.ExportMapLength; i++)
        {
            var pointer = new FPackageIndex(pkg, i + 1).ResolvedObject;
            if (pointer?.Object is null && pointer.Class?.Object?.Value is null)
                continue;

            var dummy = ((AbstractUePackage) pkg).ConstructObject(pointer.Class, pkg);
            if (dummy is not UClass || pointer.Object.Value is not UClass blueprint)
                continue;

            cppList.Add(blueprint.DecompileBlueprintToPseudo(cookedMetaData));
        }

        if (cppList.Count == 0) return false;
        var cpp = cppList.Count > 1 ? string.Join("\n\n", cppList) : cppList.FirstOrDefault() ?? string.Empty;
        if (entry.Path.Contains("_Verse.uasset"))
        {
            cpp = Regex.Replace(cpp, "__verse_0x[a-fA-F0-9]{8}_", ""); // UnmangleCasedName
        }
        cpp = Regex.Replace(cpp, @"CallFunc_([A-Za-z0-9_]+)_ReturnValue", "$1");
        cpp = Regex.Replace(cpp, @"K2Node_DynamicCast_([A-Za-z0-9_]+)", "$1");
        cpp = Regex.Replace(cpp, @"K2Node_([A-Za-z0-9_]+)", "$1");

        pseudoCpp = cpp;
        return true;
    }
}
