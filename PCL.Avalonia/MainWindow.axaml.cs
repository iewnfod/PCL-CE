using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using PCL.Core.App.IoC;
using PCL.Core.Minecraft;

namespace PCL.Avalonia;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainWindowViewModel();
    }
}

/// <summary>主窗口最小视图模型：导航占位 + 生命周期状态。</summary>
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

    public MainWindowViewModel()
    {
        var dispatcher = Dispatcher.UIThread;

        void ApplyState(LifecycleState state)
        {
            var text = state switch
            {
                LifecycleState.Running => _BuildReadyText(),
                LifecycleState.Closing or LifecycleState.Exiting => "正在退出…",
                _ => $"正在初始化（{_StateName(state)}）…"
            };
            dispatcher.Post(() => StatusText = text);
        }

        // 窗口在 WindowCreating 阶段创建，Running 尚未到达；若未来时序变化也能直接显示就绪
        if (Lifecycle.CurrentState >= LifecycleState.Running) ApplyState(LifecycleState.Running);
        else Lifecycle.StateChanged += ApplyState;
    }

    private static string _BuildReadyText()
    {
        try
        {
            var javaCount = JavaService.JavaManager.GetSortedJavaList().Count;
            return $"就绪 · 已识别 {javaCount} 个 Java";
        }
        catch
        {
            return "就绪";
        }
    }

    private static string _StateName(LifecycleState state) => state switch
    {
        LifecycleState.BeforeLoading => "预加载",
        LifecycleState.Loading => "加载基础组件",
        LifecycleState.Loaded => "初始化功能组件",
        LifecycleState.WindowCreating => "创建窗口",
        LifecycleState.WindowCreated => "窗口就绪",
        _ => state.ToString()
    };
}
