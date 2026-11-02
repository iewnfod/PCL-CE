using Avalonia;
using PCL.Core.App;
using PCL.Core.App.Essentials;
using PCL.Core.App.IoC;
using PCL.Core.Platform;
using PCL.Avalonia.Platform;

namespace PCL.Avalonia;

internal static class Program
{
    /// <summary>命令行指定的自动启动实例名（--launch &lt;name&gt;），用于自动化验证。</summary>
    public static string? AutoLaunchInstance { get; private set; }

    [STAThread]
    public static void Main()
    {
        Console.WriteLine("Welcome to Plain Craft Launcher CE (Avalonia)!");
        Console.WriteLine($"OS: {Environment.OSVersion}，PCL 位置：{Basics.ExecutablePath}");

        var args = Basics.CommandLineArguments;
        var launchIndex = Array.IndexOf(args, "--launch");
        if (launchIndex >= 0 && launchIndex + 1 < args.Length)
            AutoLaunchInstance = args[launchIndex + 1];

        // 应用容器与主窗口由生命周期服务在对应阶段装配
        ApplicationService.Loading = static () => new AvaloniaApplicationHost(AppBuilder
            .Configure<App>()
            .UsePlatformDetect()
            .LogToTrace());
        MainWindowService.Loading = static () =>
        {
            try
            {
                return new AvaloniaWindowHost(new MainWindow());
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[MainWindow] 构造失败：{ex}");
                throw;
            }
        };

        // PCL.Core 的平台服务：Avalonia 实现
        PlatformServices.UiDispatcher = new AvaloniaUiDispatcher();
        PlatformServices.Theme = new AvaloniaThemeBackend();
        PlatformServices.Localization = new AvaloniaLocalizationBackend();
        PlatformServices.Clipboard = new AvaloniaClipboardService();
        PlatformServices.DialogService = new AvaloniaDialogService();
        PlatformServices.ResourceStore = new AvaloniaResourceStore();

        // 启动链 UI 桥（LaunchUi/LoginProfile/Watcher/Loading 等）
        UiBridges.Install();

        // MC 实例目录：程序目录下 .minecraft（与核心层/WPF 版约定一致）
        GameFolders.mcFolderSelected =
            Path.Combine(Basics.ExecutableDirectory, ".minecraft");

        // 启动生命周期（含 Java 扫描、配置存储、网络等全部核心服务；内部阻塞至应用退出）
        Lifecycle.OnInitialize();
    }
}
