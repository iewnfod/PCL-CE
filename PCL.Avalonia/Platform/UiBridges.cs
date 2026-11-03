using System.Diagnostics;
using Avalonia.Threading;
using PCL.Core.Minecraft.IdentityModel;
using PCL.Core.Minecraft.IdentityModel.OAuth;
using PCL.Core.Minecraft.Profile.Authentication;
using PCL.Core.Utils.OS;

namespace PCL.Avalonia.Platform;

/// <summary>
///     UI 桥事件集线器：壳内的桥实现把事件发到这里，由 MainWindow 订阅展示。
/// </summary>
public static class UiBridgeHub
{
    /// <summary>启动日志追加（已过滤敏感信息）。</summary>
    public static event Action<string>? LaunchLogAppended;

    /// <summary>用户提示（hint / 消息框内容）。</summary>
    public static event Action<string, NotificationType>? HintShown;

    /// <summary>整体启动进度（0–1）。</summary>
    public static event Action<double>? LaunchProgressChanged;

    internal static void RaiseLaunchLog(string text) => LaunchLogAppended?.Invoke(text);
    internal static void RaiseHint(string text, NotificationType type) => HintShown?.Invoke(text, type);
    internal static void RaiseProgress(double progress) => LaunchProgressChanged?.Invoke(progress);
}

internal static class UiBridges
{
    /// <summary>注册全部启动链 UI 桥。必须在 Lifecycle.OnInitialize 之前调用。</summary>
    public static void Install()
    {
        LaunchUi.Current = new AvaloniaLaunchUi();
        LauncherUi.Interaction = new AvaloniaUserInteraction();
        LoginProfile.Current = new AvaloniaProfileUi();
        WatcherUi.Current = new AvaloniaWatcherUi();
        MediaService.Current = new NoopMediaService();
        MemorySettings.Current = new AvaloniaMemorySettings();
        LogFilter.Current = new PassThroughLogFilter();
        RestrictedFeatures.Current = new UnrestrictedFeatures();
        LoadingUi.Current = new NoopLoadingUi();
        PCL.Core.UI.Dialogs.DialogService.Instance.Presenter = new AvaloniaDialogPresenter();
        LauncherCommon.UiLogHandler = (_, text, level, title, _) =>
        {
            var type = level switch
            {
                >= LauncherLogLevel.Critical => NotificationType.Error,
                >= LauncherLogLevel.Hint => NotificationType.Warning,
                _ => NotificationType.Info
            };
            UiBridgeHub.RaiseHint($"[{title ?? "提示"}] {text}", type);
        };
        // 任务 9：档案由登录页创建（离线/微软），不再自动注入默认离线档案。
        // 无档案时左栏显示档案列表引导新建，启动按钮禁用（对齐 WPF 行为）。
    }
}

/// <summary>启动页 UI：日志接集线器，交互用对话框/控制台。</summary>
internal sealed class AvaloniaLaunchUi : ILaunchUi
{
    public int ShowMessageBox(string text, string? title = null, string? button1 = null,
        string? button2 = "", string? button3 = "", bool isWarn = false, bool forceWait = false,
        Action? button1Action = null)
    {
        Console.WriteLine($"[LaunchUi/MessageBox{(isWarn ? "/Warn" : "")}] {title}: {text}");
        UiBridgeHub.RaiseHint($"{title}: {text}", isWarn ? NotificationType.Error : NotificationType.Info);
        button1Action?.Invoke();
        return 1;
    }

    public void SetGpuPreference(string javaExePath, bool preference)
    {
        // GPU 偏好仅 Windows（内部已有平台守卫）；Unix 下由驱动/桌面环境决定
    }

    public int? ShowSelect(string caption, IReadOnlyList<string> options)
    {
        Console.WriteLine($"[LaunchUi/Select] {caption}: {string.Join(" / ", options)}");
        UiBridgeHub.RaiseHint($"{caption}（已自动选择第一项）", NotificationType.Warning);
        return 0;
    }

    public void OpenExplorer(string path)
    {
        if (OperatingSystem.IsWindows()) ProcessInterop.Start(path);
        else Process.Start(new ProcessStartInfo("xdg-open", $"\"{path}\"") { UseShellExecute = false });
    }

    public void PrepareLaunchNavigation() { }
    public void RefreshLaunchButtons() { }
    public void RefreshLaunchPage(bool show) { }

    public void AppendLaunchLog(string text) => UiBridgeHub.RaiseLaunchLog(text.TrimEnd('\r', '\n'));

    public void ClearLaunchLog() { }
    public void ShowLoginPage() { }
    public void ShowLaunchingPage() { }
    public void RefreshLaunching() { }
    public void AttachLogPage(GameWatcher.Watcher watcher) { }
    public void SetMainWindowState(LaunchWindowState state) { }
    public (double Width, double Height) GetMainPanSize() => (560, 420);
}

/// <summary>用户交互：提示走集线器，确认默认同意，网址/目录走 xdg-open。</summary>
internal sealed class AvaloniaUserInteraction : IUserInteraction
{
    public void ShowHint(string text, NotificationType type = NotificationType.Info)
    {
        Console.WriteLine($"[Hint/{type}] {text}");
        UiBridgeHub.RaiseHint(text, type);
    }

    public void ShowMessageBox(string text, string? title = null, string? button1 = null, bool isWarn = false)
    {
        Console.WriteLine($"[MessageBox{(isWarn ? "/Warn" : "")}] {title}: {text}");
        UiBridgeHub.RaiseHint($"{title}: {text}", isWarn ? NotificationType.Error : NotificationType.Info);
    }

    public void OpenWebsite(string url)
    {
        if (OperatingSystem.IsWindows()) ProcessInterop.Start(url);
        else Process.Start(new ProcessStartInfo("xdg-open", $"\"{url}\"") { UseShellExecute = false });
    }

    public bool ShowConfirm(string text, string? title = null, string? confirmText = null, string? cancelText = null)
    {
        Console.WriteLine($"[Confirm] {title}: {text}（默认同意）");
        return true;
    }
}

/// <summary>游戏监视器 UI：退出走生命周期，提示走集线器。</summary>
internal sealed class AvaloniaWatcherUi : IWatcherUi
{
    public void RefreshShutdownButton() { }

    public void ExitApplication()
    {
        Console.WriteLine("[Watcher] 游戏退出，请求关闭启动器");
        Dispatcher.UIThread.Post(() => PCL.Core.App.IoC.Lifecycle.Shutdown());
    }

    public void ShowApplication() { }
    public void ShowFeedback() { }

    public void ShowHint(string text, NotificationType type = NotificationType.Info)
    {
        Console.WriteLine($"[Watcher/Hint/{type}] {text}");
        UiBridgeHub.RaiseHint(text, type);
    }
}

internal sealed class NoopMediaService : IMediaService
{
    public void PauseMusic() { }
    public void ResumeMusic() { }
    public void SetVideoGaming(bool gaming) { }
}

internal sealed class PassThroughLogFilter : ILogFilter
{
    public string FilterUserName(string raw, char filterChar) => raw;
    public string FilterAccessToken(string raw, char filterChar) => raw;
}

/// <summary>MVP 不做区域限制。</summary>
internal sealed class UnrestrictedFeatures : IRestrictedFeatures
{
    public bool IsRestricted => false;
    public bool Check(string feature) => false;
    public void ThrowIfRestricted(string feature) { }
}

/// <summary>加载任务 UI：仅把整体进度发到集线器。</summary>
internal sealed class NoopLoadingUi : ILoadingUi
{
    public void TaskAdded(PCL.Loading.LoaderBase loader) { }
    public void TaskRemoved(PCL.Loading.LoaderBase loader) { }
    public void TaskRefreshed(PCL.Loading.LoaderBase loader) { }
    public void RefreshDownloadButton() { }
    public void UpdateButtonProgress(double progress) => UiBridgeHub.RaiseProgress(progress);
    public void UpdateTaskbar(TaskbarProgressState state, double progress) { }
}
