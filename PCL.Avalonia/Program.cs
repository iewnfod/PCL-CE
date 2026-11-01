using Avalonia;
using PCL.Core.App;
using PCL.Core.App.Essentials;
using PCL.Core.App.IoC;
using PCL.Core.Platform;
using PCL.Avalonia.Platform;

namespace PCL.Avalonia;

internal static class Program
{
    [STAThread]
    public static void Main()
    {
        Console.WriteLine("Welcome to Plain Craft Launcher CE (Avalonia)!");
        Console.WriteLine($"OS: {Environment.OSVersion}，PCL 位置：{Basics.ExecutablePath}");

        // 应用容器与主窗口由生命周期服务在对应阶段装配
        ApplicationService.Loading = static () => new AvaloniaApplicationHost(AppBuilder
            .Configure<App>()
            .UsePlatformDetect()
            .LogToTrace());
        MainWindowService.Loading = static () => new AvaloniaWindowHost(new MainWindow());

        // PCL.Core 的平台服务：Avalonia 实现
        PlatformServices.UiDispatcher = new AvaloniaUiDispatcher();
        PlatformServices.Theme = new AvaloniaThemeBackend();
        PlatformServices.Localization = new AvaloniaLocalizationBackend();
        PlatformServices.Clipboard = new AvaloniaClipboardService();
        PlatformServices.DialogService = new AvaloniaDialogService();
        PlatformServices.ResourceStore = new AvaloniaResourceStore();

        // 启动生命周期（含 Java 扫描、配置存储、网络等全部核心服务）
        Lifecycle.OnInitialize();
    }
}
