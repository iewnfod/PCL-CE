using System.IO;
using PCL.Core.App.Localization;
using PCL.Core.Utils;
using PCL.Network;
using PCL.Network.Loaders;
using PCL.Text;

namespace PCL.Avalonia;

public static partial class DownloadUi
{
    /// <summary>
    ///     安装收尾：合并多实例 JSON、拷贝临时 libraries 与 mods 文件夹（WPF DownloadUi/InstallMerge.cs 移植）。
    /// </summary>
    private static Loading.LoaderTask<string, string> _CreateMergeInstallLoader(
        string instanceFolder,
        string tempMcFolder,
        McInstallFolders folders,
        McInstallRequest request
    )
    {
        return new Loading.LoaderTask<string, string>(
            Lang.Text("Minecraft.Download.Stage.InstallGame"),
            task =>
            {
                MergeJson(
                    instanceFolder,
                    instanceFolder,
                    folders.ForgeFolder,
                    request.forgeVersion,
                    folders.NeoForgeFolder,
                    request.neoForgeVersion,
                    folders.CleanroomFolder,
                    request.cleanroomVersion,
                    folders.FabricFolder,
                    request.mmcPackInfo);
                task.Progress = 0.2d;
                if (Directory.Exists(Path.Combine(tempMcFolder, "libraries")))
                    LauncherCommon.CopyDirectory(
                        Path.Combine(tempMcFolder, "libraries"),
                        Path.Combine(GameFolders.mcFolderSelected ?? "", "libraries"));
                task.Progress = 0.8d;
                var modsFolder = Path.Combine(new McInstance(instanceFolder).PathIndie, "mods");
                if (Directory.Exists(folders.ModsTempFolder))
                {
                    LauncherCommon.CopyDirectory(folders.ModsTempFolder, modsFolder);
                }
                else if (folders.Modable)
                {
                    Directory.CreateDirectory(modsFolder);
                    LauncherCommon.Log($"[Download] 自动创建 Mod 文件夹：{modsFolder}");
                }

                var resourcepacksFolder = Path.Combine(
                    new McInstance(instanceFolder).PathIndie,
                    "resourcepacks");
                Directory.CreateDirectory(resourcepacksFolder);
                LauncherCommon.Log($"[Download] 自动创建资源包文件夹：{resourcepacksFolder}");
            }
        )
        {
            ProgressWeight = 2d,
            block = true
        };
    }

    private static bool _ShouldFixInstallLibraries(McInstallRequest request)
    {
        return request.mmcPackInfo is not null
            || (request.forgeVersion is not null
                && Convert.ToDouble(request.forgeVersion.BeforeFirst(".")) >= 20d)
            || request.neoForgeVersion is not null
            || request.fabricVersion is not null
            || request.cleanroomVersion is not null;
    }

    private static void _AddLibraryFixLoader(
        List<Loading.LoaderBase> loaderList,
        McInstallRequest request,
        string instanceFolder,
        string minecraftName)
    {
        var loadersLib = new List<Loading.LoaderBase>
        {
            new Loading.LoaderTask<string, List<DownloadFile>>(
                Lang.Text("Minecraft.Download.Stage.AnalyzeGameLibrariesSide"),
                task => task.output =
                    Libraries.McLibNetFilesFromInstance(new McInstance(instanceFolder))
            )
            {
                ProgressWeight = 1d,
                show = false
            },
            new LoaderDownload(Lang.Text("Minecraft.Download.Stage.DownloadGameLibrariesSide"), [])
            {
                ProgressWeight = 7d,
                show = false
            }
        };
        loaderList.Add(
            new Loading.LoaderCombo<string>(
                Lang.Text("Minecraft.Download.Stage.DownloadGameLibraries"),
                loadersLib)
            {
                ProgressWeight = 8d
            }
        );
    }
}
