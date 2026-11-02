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
        LoginProfile.Current = new OfflineProfileUi("Player");
        WatcherUi.Current = new AvaloniaWatcherUi();
        MediaService.Current = new NoopMediaService();
        MemorySettings.Current = new DefaultMemorySettings();
        LogFilter.Current = new PassThroughLogFilter();
        RestrictedFeatures.Current = new UnrestrictedFeatures();
        LoadingUi.Current = new NoopLoadingUi();
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

        // MVP 默认档案：档案服务就绪后若无选中档案，注入一个离线档案（玩家名 Player）
        PCL.Core.App.IoC.Lifecycle.When(PCL.Core.App.IoC.LifecycleState.Running, () =>
        {
            if (PCL.Core.Minecraft.Profile.ProfileService.Current is not null) return;
            var uuid = OfflineProfileUi._GetOfflineUuid("Player");
            PCL.Core.Minecraft.Profile.ProfileService.Add(new PCL.Core.Minecraft.Profile.Models.McProfile
            {
                UserName = "Player",
                Uuid = uuid,
                AccessToken = uuid,
                TokenType = "Legacy",
                ProfileType = PCL.Core.Minecraft.Profile.Models.ProfileType.Offline
            }, select: true);
            Console.WriteLine("[Profile] 已注入默认离线档案：Player");
        });
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

/// <summary>离线登录档案：固定玩家名，UUID 采用离线服务器标准算法（MD5("OfflinePlayer:"+name)）。</summary>
internal sealed class OfflineProfileUi(string playerName) : IProfileUi
{
    public object? GetLoginData() => new GameLaunch.McLoginLegacy
    {
        LoginType = GameLaunch.McLoginType.Legacy,
        UserName = playerName,
        Uuid = _GetOfflineUuid(playerName)
    };

    public string IsProfileValid() => "";

    public Task<AuthorizeResult?> ShowDeviceCodeLoginAsync(DeviceCodeAuthenticationContext context,
        CancellationToken token)
    {
        // 离线档案不触发微软登录
        return Task.FromResult<AuthorizeResult?>(null);
    }

    public bool HandleMicrosoftXstsError(IdentityModelAuthenticationException exception) => false;
    public bool HandleMicrosoftNotOwnedError(IdentityModelAuthenticationException exception) => false;
    public bool HandleMicrosoftCreateProfileError(IdentityModelAuthenticationException exception) => false;

    /// <summary>与离线服务器一致的离线 UUID 算法：UUID v3(MD5, "OfflinePlayer:" + name)。</summary>
    internal static string _GetOfflineUuid(string name)
    {
        var md5 = System.Security.Cryptography.MD5.HashData(
            System.Text.Encoding.UTF8.GetBytes("OfflinePlayer:" + name));
        // RFC 4122 version 3 变换
        md5[6] = (byte)((md5[6] & 0x0F) | 0x30);
        md5[8] = (byte)((md5[8] & 0x3F) | 0x80);
        return new Guid(md5).ToString();
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

/// <summary>内存分配：默认策略（总内存的 40%，上限 8G，下限 1G，32 位 Java 上限 1G）。</summary>
internal sealed class DefaultMemorySettings : IMemorySettings
{
    public double GetRam(McInstance version, bool? is32BitJava = null)
    {
        var totalGb = KernelInterop.GetPhysicalMemoryBytes().Total / 1024.0 / 1024 / 1024;
        var ram = Math.Clamp(totalGb * 0.4, 1, 8);
        if (is32BitJava == true) ram = Math.Min(ram, 1);
        return Math.Floor(ram * 10) / 10;
    }

    public double? GetInitialRam(McInstance version, bool? is32BitJava = null) => null;
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
