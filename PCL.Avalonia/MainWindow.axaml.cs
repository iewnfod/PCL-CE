using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using PCL.Core.App.IoC;

namespace PCL.Avalonia;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainWindowViewModel();
    }
}

/// <summary>主窗口最小视图模型：导航占位 + 状态文本。</summary>
public partial class MainWindowViewModel : INotifyPropertyChanged
{
    private string _statusText = "正在初始化…";

    public string StatusText
    {
        get => _statusText;
        private set { _statusText = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(StatusText))); }
    }

    public object CurrentPage { get; } = new Grid
    {
        Children =
        {
            new StackPanel
            {
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                Spacing = 8,
                Children =
                {
                    new TextBlock { Text = "PCL CE · Avalonia", FontSize = 28, FontWeight = FontWeight.Bold },
                    new TextBlock { Text = "壳骨架运行中 —— 页面将在后续任务逐步移植", Opacity = 0.6 }
                }
            }
        }
    };

    public event PropertyChangedEventHandler? PropertyChanged;

    public void NavigateHome() { }
    public void NavigateLaunch() { }
    public void NavigateDownload() { }
    public void NavigateSetup() { }
}
