using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using PCL.Core.App.IoC;

namespace PCL.Avalonia;

public class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        // 生命周期在 Program.Main 手动驱动（OnInitialize → Run → Startup → OnLoading），
        // 此处不做窗口创建，避免与 MainWindowService 冲突
        base.OnFrameworkInitializationCompleted();
    }
}
