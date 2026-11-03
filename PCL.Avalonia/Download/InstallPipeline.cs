using System.IO;
using PCL.Core.App;
using PCL.Core.App.Configuration;
using PCL.Core.App.Configuration.Storage;
using PCL.Core.App.Localization;
using PCL.Core.UI.Hints;

namespace PCL.Avalonia;

/// <summary>
///     合并安装管线（WPF DownloadUi 的跨平台移植，MVP 子集：原版 / Forge / NeoForge / Cleanroom / Fabric）。<br/>
///     差异：Common → LauncherCommon；路径拼接全部改为 Path API；去掉 frmMain 直连与 LabyMod / LiteLoader / OptiFine 分支。
/// </summary>
public static partial class DownloadUi
{
    private const string McInstallDefaultType = "安装";

    /// <summary>如果两个 Forgelike 同时开始安装，就会导致安装失败。</summary>
    private static readonly object InstallSyncLock = new();

    /// <summary>如果两个 Forgelike 同时复制原版 Jar，就会导致复制文件时冲突。</summary>
    private static readonly object VanillaSyncLock = new();

    private const string McDownloadClientLibId = "VanillaLibrariesDownload";
    private const string McDownloadClientJsonId = "VanillaJsonDownload";

    private static readonly char DirSep = Path.DirectorySeparatorChar;

    /// <summary>versions 下的实例文件夹绝对路径，以目录分隔符结尾（对齐 WPF 的 $@"...\{name}\" 形态）。</summary>
    internal static string GetInstanceFolderPath(string instanceName)
    {
        return Path.Combine(GameFolders.mcFolderSelected ?? "", "versions", instanceName) + DirSep;
    }

    /// <summary>在加载器状态改变后显示一条提示，不进行任何其他操作。</summary>
    public static void LoaderStateChangedHintOnly(object loaderObj)
    {
        var loader = (Loading.LoaderBase)loaderObj;
        switch (loader.State)
        {
            case LoadState.Finished:
                HintService.Show(Hints.Success($"{loader.name}{Lang.Text("Common.Status.Success")}"));
                break;
            case LoadState.Failed:
                HintService.Show(Hints.Error($"{loader.name}{Lang.Text("Common.Status.Failure")}{loader.Error.Message}"));
                break;
            case LoadState.Aborted:
                HintService.Show(Hints.Info($"{loader.name}{Lang.Text("Common.Status.Cancelled")}"));
                break;
            case LoadState.Waiting:
            case LoadState.Loading:
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    /// <summary>安装加载器状态改变后进行提示和重载文件夹列表的方法。</summary>
    public static void McInstallState(object loaderObj)
    {
        var loader = (Loading.LoaderBase)loaderObj;
        var combo = (Loading.LoaderCombo)loader;
        switch (loader.State)
        {
            case LoadState.Finished:
                _HandleInstallFinished(loader);
                break;
            case LoadState.Failed:
                HintService.Show(Hints.Error($"{loader.name}{Lang.Text("Common.Status.Failure")}{loader.Error.Message}"));
                break;
            case LoadState.Aborted:
                HintService.Show(Hints.Info($"{loader.name}{Lang.Text("Common.Status.Cancelled")}"));
                break;
            case LoadState.Loading:
                return;
            case LoadState.Waiting:
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }

        _RestoreOrClearInstallFolder(loader, combo);
        Loading.LoaderFolderRun(
            InstanceList.mcInstanceListLoader,
            GameFolders.mcFolderSelected ?? "",
            Loading.LoaderFolderRunType.ForceRun,
            1,
            "versions" + DirSep);
    }

    private static void _HandleInstallFinished(Loading.LoaderBase loader)
    {
        if (Config.Download.AutoSelectInstance)
        {
            var versionName = loader.name;
            LauncherCommon.WriteIni(
                Path.Combine(GameFolders.mcFolderSelected ?? "", "PCL.ini"),
                "Version",
                versionName.Remove(versionName.Length - 3, 3));
        }

        LauncherCommon.WriteIni(Path.Combine(GameFolders.mcFolderSelected ?? "", "PCL.ini"), "InstanceCache", "");
        LauncherCommon.DeleteDirectory(
            Path.Combine(((Loading.LoaderCombo)loader).input?.ToString() ?? "", "PCLInstallBackups"));
        HintService.Show(Hints.Success($"{loader.name}{Lang.Text("Common.Status.Success")}"));
    }

    private static void _RestoreOrClearInstallFolder(
        Loading.LoaderBase loader,
        Loading.LoaderCombo combo)
    {
        var backupFolder = Path.Combine(combo.input?.ToString() ?? "", "PCLInstallBackups");
        if (loader.State != LoadState.Finished && Directory.Exists(backupFolder))
        {
            LauncherCommon.CopyDirectory(backupFolder, combo.input?.ToString() ?? "");
            File.Delete(Path.Combine(combo.input?.ToString() ?? "", ".pclignore"));
            LauncherCommon.DeleteDirectory(backupFolder);
        }
        else
        {
            McInstallFailedClearFolder(loader);
        }
    }

    public static void McInstallFailedClearFolder(object loader)
    {
        try
        {
            if (((Loading.LoaderBase)loader).State is not (LoadState.Failed or LoadState.Aborted))
                return;

            Thread.Sleep(1000); // 防止存在尚未完全释放的文件，导致清理失败（例如整合包安装）
            LauncherCommon.Log(
                $"[Download] 由于下载失败或取消，清理实例文件夹：{((Loading.LoaderCombo)loader).input}",
                LauncherLogLevel.Developer);
            var instancePath = ((Loading.LoaderCombo)loader).input?.ToString() ?? "";
            ((DynamicCacheConfigStorage)ConfigService.GetProvider(ConfigSource.GameInstance))
                .InvalidateCache(instancePath);
            LauncherCommon.DeleteDirectory(instancePath);
        }
        catch (Exception ex)
        {
            LauncherCommon.Log(ex, "下载失败或取消后清理实例文件夹失败");
        }
    }

    /// <summary>
    ///     进行合并安装。返回是否已经开始安装。
    /// </summary>
    public static bool McInstall(McInstallRequest request, string type = McInstallDefaultType)
    {
        try
        {
            LauncherCommon.Log("[Diag] McInstall: 构建 loader 列表", LauncherLogLevel.Debug);
            var subLoaders = McInstallLoader(request, ignoreDump: type != McInstallDefaultType);
            if (subLoaders is null)
                return false;

            LauncherCommon.Log($"[Diag] McInstall: loader 列表构建完成（{subLoaders.Count} 项），启动", LauncherLogLevel.Debug);
            _StartMcInstallLoader(request, subLoaders, type);
            LauncherCommon.Log("[Diag] McInstall: 启动完成", LauncherLogLevel.Debug);
            return true;
        }
        catch (CancelledException)
        {
            return false;
        }
        catch (Exception ex)
        {
            LauncherCommon.Log(
                ex,
                "开始合并安装失败",
                LauncherLogLevel.Feedback,
                userSummary: Lang.Text("Minecraft.Download.Error.OperationFailed"));
            _ClearIncompleteInstallFolder(request.targetInstanceFolder);
            return false;
        }
    }

    private static void _StartMcInstallLoader(
        McInstallRequest request,
        List<Loading.LoaderBase> subLoaders,
        string type)
    {
        var loader = new Loading.LoaderCombo<string>(
            $"{request.targetInstanceName} {type}",
            subLoaders)
        {
            OnStateChanged = McInstallState
        };

        loader.Start(request.targetInstanceFolder);
        Loading.LoaderTaskbarAdd(loader);
    }

    private static void _ClearIncompleteInstallFolder(string instanceFolder)
    {
        try
        {
            if (!Directory.Exists(instanceFolder))
                return;

            var files = Directory.GetFiles(instanceFolder);
            var dirs = Directory.GetDirectories(instanceFolder);
            if (files.Length > 1 || dirs.Length != 0)
                return;

            ((DynamicCacheConfigStorage)ConfigService.GetProvider(ConfigSource.GameInstance))
                .InvalidateCache(instanceFolder);
            LauncherCommon.DeleteDirectory(instanceFolder);
        }
        catch (Exception innerEx)
        {
            LauncherCommon.Log(innerEx, "清理未完成的实例文件夹失败");
        }
    }

    /// <summary>等待文件就绪（WPF Common.WaitForFileReady 的等价实现）。</summary>
    private static void WaitForFileReady(string filePath, int timeoutMs = 2000)
    {
        var deadline = Environment.TickCount64 + timeoutMs;
        while (Environment.TickCount64 < deadline)
            try
            {
                if (!File.Exists(filePath))
                    return;
                using var _ = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                return;
            }
            catch (IOException)
            {
                Thread.Sleep(20);
            }
            catch (UnauthorizedAccessException)
            {
                Thread.Sleep(20);
            }
    }

    /// <summary>安装过程中的各临时文件夹。</summary>
    private sealed class McInstallFolders
    {
        public string? CleanroomFolder;
        public string? FabricFolder;
        public string? ForgeFolder;
        public bool Modable;
        public string ModsTempFolder = "";
        public string? NeoForgeFolder;
    }
}
