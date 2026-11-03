using System.IO;
using System.Text.Json.Nodes;
using PCL.Core.App.Localization;
using PCL.Core.Utils;
using PCL.Text;

namespace PCL.Avalonia;

public static partial class DownloadUi
{
    /// <summary>
    ///     将多个实例 JSON 进行合并，如果目标已存在则直接覆盖。失败会抛出异常。<br/>
    ///     WPF DownloadUi/MergeJson*.cs 移植；MVP 子集：Minecraft / Forge / NeoForge / Cleanroom / Fabric。
    /// </summary>
    private static void MergeJson(
        string outputFolder,
        string minecraftFolder,
        string? forgeFolder = null,
        string? forgeVersion = null,
        string? neoForgeFolder = null,
        string? neoForgeVersion = null,
        string? cleanroomFolder = null,
        string? cleanroomVersion = null,
        string? fabricFolder = null,
        MmcPackInfo? mmcPackInfo = null
    )
    {
        LauncherCommon.Log(
            $"[Download] 开始进行实例合并，输出：{outputFolder}，Minecraft：{minecraftFolder}" +
            $"{(forgeFolder is not null ? $"，Forge：{forgeFolder}" : "")}" +
            $"{(neoForgeFolder is not null ? $"，NeoForge：{neoForgeFolder}" : "")}" +
            $"{(cleanroomFolder is not null ? $"，Cleanroom：{cleanroomFolder}" : "")}" +
            $"{(fabricFolder is not null ? $"，Fabric：{fabricFolder}" : "")}"
        );
        Directory.CreateDirectory(outputFolder);

        var paths = _PrepareMergeJsonPaths(
            outputFolder,
            minecraftFolder,
            forgeFolder,
            neoForgeFolder,
            cleanroomFolder,
            fabricFolder);
        var jsons = _LoadMergeJsonFiles(paths);

        var allArguments = $"{_GetMinecraftArguments(jsons.Minecraft)} "
            + $"{_GetMinecraftArguments(jsons.Forge)} "
            + $"{_GetMinecraftArguments(jsons.NeoForge)} "
            + $"{_GetMinecraftArguments(jsons.Cleanroom)}";
        var realArguments = _SplitMergeArguments(allArguments).Distinct().ToList().Join(" ");

        var outputJson = _CreateMergedOutputJson(jsons.Minecraft, mmcPackInfo) ?? new JsonObject();

        _MergeLoaderJson(outputJson, jsons.Forge, mmcPackInfo?.isForgeOverrided ?? false);
        _MergeLoaderJson(outputJson, jsons.NeoForge, mmcPackInfo?.isNeoForgeOverrided ?? false);
        _MergeLoaderJson(outputJson, jsons.Cleanroom, mmcPackInfo?.isCleanroomOverrided ?? false);
        _MergeLoaderJson(outputJson, jsons.Fabric, mmcPackInfo?.isFabricOverrided ?? false);

        _RemoveConflictingLwjgl2(outputJson);

        if (realArguments is not null && !string.IsNullOrEmpty(realArguments.Replace(" ", "")))
            outputJson["minecraftArguments"] = realArguments;
        if (mmcPackInfo is not null && mmcPackInfo.isMcArgsEdited)
            outputJson.Remove("minecraftArguments");
        outputJson.Remove("_comment_");
        outputJson.Remove("inheritsFrom");
        outputJson.Remove("jar");
        outputJson["id"] = paths.OutputName;

        LauncherCommon.WriteFile(paths.OutputJsonPath, outputJson.ToString());
        if ((paths.MinecraftJar ?? "") != (paths.OutputJar ?? ""))
        {
            if (File.Exists(paths.OutputJar))
                File.Delete(paths.OutputJar);
            LauncherCommon.CopyFile(
                paths.MinecraftJar ?? throw new InvalidOperationException("Minecraft Jar 路径无效"),
                paths.OutputJar ?? throw new InvalidOperationException("输出 Jar 路径无效"));
        }

        LauncherCommon.Log($"[Download] 实例合并 {paths.OutputName} 完成");
    }

    private static MergeJsonPaths _PrepareMergeJsonPaths(
        string outputFolder,
        string minecraftFolder,
        string? forgeFolder,
        string? neoForgeFolder,
        string? cleanroomFolder,
        string? fabricFolder
    )
    {
        var paths = new MergeJsonPaths();

        outputFolder = _NormalizeVersionFolder(outputFolder);
        paths.OutputName = LauncherCommon.GetFolderNameFromPath(outputFolder);
        paths.OutputJsonPath = Path.Combine(outputFolder, $"{paths.OutputName}.json");
        paths.OutputJar = Path.Combine(outputFolder, $"{paths.OutputName}.jar");

        minecraftFolder = _NormalizeVersionFolder(minecraftFolder);
        var minecraftName = LauncherCommon.GetFolderNameFromPath(minecraftFolder);
        paths.MinecraftJsonPath = Path.Combine(minecraftFolder, $"{minecraftName}.json");
        paths.MinecraftJar = Path.Combine(minecraftFolder, $"{minecraftName}.jar");

        if (forgeFolder is not null)
            paths.ForgeJsonPath = _PrepareVersionFolder(ref forgeFolder);
        if (neoForgeFolder is not null)
            paths.NeoForgeJsonPath = _PrepareVersionFolder(ref neoForgeFolder);
        if (cleanroomFolder is not null)
            paths.CleanroomJsonPath = _PrepareVersionFolder(ref cleanroomFolder);
        if (fabricFolder is not null)
            paths.FabricJsonPath = _PrepareVersionFolder(ref fabricFolder);

        return paths;
    }

    private static MergeJsonFiles _LoadMergeJsonFiles(MergeJsonPaths paths)
    {
        var files = new MergeJsonFiles
        {
            Minecraft = _ReadMergeJson(paths.MinecraftJsonPath, "Minecraft")
        };

        if (paths.ForgeJsonPath is not null)
            files.Forge = _ReadMergeJson(paths.ForgeJsonPath, "Forge");
        if (paths.NeoForgeJsonPath is not null)
            files.NeoForge = _ReadMergeJson(paths.NeoForgeJsonPath, "NeoForge");
        if (paths.CleanroomJsonPath is not null)
            files.Cleanroom = _ReadMergeJson(paths.CleanroomJsonPath, "Cleanroom");
        if (paths.FabricJsonPath is not null)
            files.Fabric = _ReadMergeJson(paths.FabricJsonPath, "Fabric");

        return files;
    }

    /// <summary>规范化实例文件夹：保证以平台目录分隔符结尾（替代 WPF 版的 <c>@"\"</c> 拼接）。</summary>
    private static string _NormalizeVersionFolder(string folder)
    {
        return folder.EndsWithF(DirSep.ToString()) ? folder : folder + DirSep;
    }

    private static string _PrepareVersionFolder(ref string folder)
    {
        folder = _NormalizeVersionFolder(folder);
        var folderName = LauncherCommon.GetFolderNameFromPath(folder);
        return Path.Combine(folder, $"{folderName}.json");
    }

    private static JsonObject _ReadMergeJson(
        string jsonPath,
        string sourceName,
        string? reportedPath = null)
    {
        var jsonText = LauncherCommon.ReadFile(jsonPath);
        if (!jsonText.StartsWithF("{"))
            throw new Exception(
                Lang.Text(
                    "Minecraft.Download.Error.JsonInvalid",
                    sourceName,
                    reportedPath ?? jsonPath,
                    jsonText.Substring(0, Math.Min(jsonText.Length, 1000))
                )
            );
        return (JsonObject)LauncherCommon.GetJson(jsonText);
    }

    private static string _GetMinecraftArguments(JsonObject? json)
    {
        return (json?["minecraftArguments"] ?? " ").ToString();
    }

    private static List<string> _SplitMergeArguments(string allArguments)
    {
        var rawArguments = allArguments
            .Split(" ")
            .Where(l => !string.IsNullOrEmpty(l))
            .Select(l => l.Trim())
            .ToList();
        var splitArguments = new List<string>();
        for (var i = 0; i < rawArguments.Count; i++)
            if (rawArguments[i].StartsWithF("-"))
                splitArguments.Add(rawArguments[i]);
            else if (splitArguments.Count != 0
                     && splitArguments.Last().StartsWithF("-")
                     && !splitArguments.Last().Contains(' '))
                splitArguments[^1] = $"{splitArguments.Last()} {rawArguments[i]}";
            else
                splitArguments.Add(rawArguments[i]);

        return splitArguments;
    }

    private static JsonObject? _CreateMergedOutputJson(
        JsonObject? minecraftJson,
        MmcPackInfo? mmcPackInfo)
    {
        if (mmcPackInfo is null)
            return minecraftJson;

        if (!mmcPackInfo.isMinecraftOverrided)
        {
            LauncherCommon.Log("[Download] 存在无修改 MC 核心文件的 MMC 整合包信息，应用相关参数");
            minecraftJson?.Merge(mmcPackInfo.overridedJson);
            return minecraftJson;
        }

        LauncherCommon.Log("[Download] 当前实例的 MC 核心已被修改，使用对应的 MMC 整合包参数");
        var overriddenJson = minecraftJson is not null
            ? (JsonObject)minecraftJson.DeepClone()
            : new JsonObject();
        foreach (var patchProperty in mmcPackInfo.overridedJson)
            overriddenJson[patchProperty.Key] = patchProperty.Value?.DeepClone();
        return overriddenJson;
    }

    private static void _MergeLoaderJson(
        JsonObject outputJson,
        JsonObject? loaderJson,
        bool isOverridden)
    {
        if (loaderJson is null || isOverridden)
            return;

        loaderJson.Remove("releaseTime");
        loaderJson.Remove("time");
        outputJson.Merge(loaderJson);
    }

    private static void _RemoveConflictingLwjgl2(JsonObject outputJson)
    {
        if (outputJson["libraries"] is not JsonArray finalLibs || !finalLibs.Any(IsLwjgl3Library))
            return;

        for (var i = finalLibs.Count - 1; i >= 0; i--)
            if (IsLwjgl2Library(finalLibs[i]))
            {
                LauncherCommon.Log(
                    $"[Download] 已移除与 LWJGL3 冲突的 LWJGL2 支持库：{finalLibs[i]?["name"]}");
                finalLibs.RemoveAt(i);
            }
    }

    private static bool IsLwjgl3Library(JsonNode? library)
    {
        if (library is not JsonObject lib)
            return false;
        var segments = (lib["name"]?.ToString() ?? "").Split(':');
        return segments is ["org.lwjgl", _, _, ..]
               && segments[2].StartsWith("3.", StringComparison.Ordinal);
    }

    private static bool IsLwjgl2Library(JsonNode? library)
    {
        if (library is not JsonObject lib)
            return false;
        var segments = (lib["name"]?.ToString() ?? "").Split(':');
        return segments.Length >= 3
               && (segments[0] == "org.lwjgl.lwjgl"
                   || (segments[0] == "org.lwjgl"
                       && segments[2].StartsWith("2.", StringComparison.Ordinal)));
    }

    private sealed class MergeJsonPaths
    {
        public string? CleanroomJsonPath;
        public string? FabricJsonPath;
        public string? ForgeJsonPath;
        public string MinecraftJar = "";
        public string MinecraftJsonPath = "";
        public string? NeoForgeJsonPath;
        public string OutputJar = "";
        public string OutputJsonPath = "";
        public string OutputName = "";
    }

    private sealed class MergeJsonFiles
    {
        public JsonObject? Cleanroom;
        public JsonObject? Fabric;
        public JsonObject? Forge;
        public JsonObject? Minecraft;
        public JsonObject? NeoForge;
    }
}
