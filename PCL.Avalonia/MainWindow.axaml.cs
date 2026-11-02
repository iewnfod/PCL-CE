using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using PCL.Avalonia.Platform;
using PCL.Avalonia.Controls;
using PCL.Core.App.IoC;
using PCL.Core.Minecraft;

namespace PCL.Avalonia;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        var viewModel = new MainWindowViewModel();
        DataContext = viewModel;
        viewModel.InitUi(this);
        Loaded += (_, _) => HintPresenterBridge.Install(this);
    }
}

/// <summary>主窗口视图模型：实例选择、启动控制、日志与状态。</summary>
public partial class MainWindowViewModel : INotifyPropertyChanged
{
    private string _statusText = "正在初始化…";
    private string _launchLog = "";
    private string _hintText = "";
    private double _launchProgress;
    private bool _isLaunching;
    private ComboBox? _instanceCombo;

    public string StatusText
    {
        get => _statusText;
        private set { _statusText = value; OnPropertyChanged(nameof(StatusText)); }
    }

    public string LaunchLog
    {
        get => _launchLog;
        private set { _launchLog = value; OnPropertyChanged(nameof(LaunchLog)); }
    }

    public string HintText
    {
        get => _hintText;
        private set { _hintText = value; OnPropertyChanged(nameof(HintText)); }
    }

    public double LaunchProgress
    {
        get => _launchProgress;
        private set { _launchProgress = value; OnPropertyChanged(nameof(LaunchProgress)); }
    }

    public bool IsLaunching
    {
        get => _isLaunching;
        private set { _isLaunching = value; OnPropertyChanged(nameof(IsLaunching)); }
    }

    public ObservableCollection<string> InstanceNames { get; } = [];

    /// <summary>控件库演示卡片（任务 5 验收用，默认折叠）。</summary>
    public Controls.MyCard DemoCard { get; } = new()
    {
        Title = "控件库演示（点击展开）",
        CanSwap = true,
        IsSwapped = true,
    };

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged(string name) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    /// <summary>绑定 UI 控件引用并挂事件（AXAML 绑定之外的命令式部分）。</summary>
    public void InitUi(Window window)
    {
        _instanceCombo = window.FindControl<ComboBox>("InstanceCombo");
        _BuildDemoContent();

        UiBridgeHub.LaunchLogAppended += text => Dispatcher.UIThread.Post(() =>
        {
            LaunchLog += (LaunchLog.Length == 0 ? "" : "\n") + text;
            if (LaunchLog.Length > 200_000) LaunchLog = LaunchLog[^100_000..];
        });
        UiBridgeHub.HintShown += (text, type) => Dispatcher.UIThread.Post(() =>
            HintText = $"{DateTime.Now:HH:mm:ss} [{type}] {text}");
        UiBridgeHub.LaunchProgressChanged += progress => Dispatcher.UIThread.Post(() => LaunchProgress = progress);

        // 生命周期状态
        void ApplyState(LifecycleState state)
        {
            var text = state switch
            {
                LifecycleState.Running => _BuildReadyText(),
                LifecycleState.Closing or LifecycleState.Exiting => "正在退出…",
                _ => $"正在初始化（{_StateName(state)}）…"
            };
            Dispatcher.UIThread.Post(() => StatusText = text);
        }

        if (Lifecycle.CurrentState >= LifecycleState.Running) ApplyState(LifecycleState.Running);
        else Lifecycle.StateChanged += ApplyState;

        // 就绪后加载实例列表
        Lifecycle.When(LifecycleState.Running, () =>
        {
            RefreshInstances();

            // 自动启动（--launch <实例名>）：用于端到端自动化验证
            if (Program.AutoLaunchInstance is { } name && InstanceNames.Contains(name))
            {
                Dispatcher.UIThread.Post(() =>
                {
                    if (_instanceCombo is not null)
                        _instanceCombo.SelectedItem = name;
                    Launch();
                });
            }
        });
    }

    public void RefreshInstances()
    {
        InstanceNames.Clear();
        try
        {
            // 扫描 MinecraftFolder/versions 下的有效实例
            var versionsDir = Path.Combine(
                GameFolders.mcFolderSelected ?? "", "versions");
            if (Directory.Exists(versionsDir))
            {
                var names = Directory.GetDirectories(versionsDir)
                    .Select(Path.GetFileName)
                    .Where(n => n is not null)
                    .OrderBy(n => n)
                    .ToList();
                foreach (var name in names!) InstanceNames.Add(name);
            }
        }
        catch (Exception ex)
        {
            HintText = $"扫描实例失败：{ex.Message}";
        }

        if (InstanceNames.Count == 0)
            HintText = $"未找到实例：请将版本文件夹放入 {GameFolders.mcFolderSelected}/versions/ 后点「刷新」";
    }

    public void Launch()
    {
        if (IsLaunching)
        {
            HintText = "已有启动任务正在进行";
            return;
        }

        var name = _instanceCombo?.SelectedItem as string;
        if (string.IsNullOrEmpty(name))
        {
            HintText = "请先选择实例";
            return;
        }

        try
        {
            var instance = new McInstance(name);
            instance.Load();
            if (instance.state == McInstanceState.Error)
            {
                HintText = $"实例不可用：{instance.Desc}";
                return;
            }

            IsLaunching = true;
            LaunchProgress = 0;
            StatusText = "正在启动…";

            var ok = GameLaunch.McLaunchStart(new GameLaunch.McLaunchOptions { instance = instance });
            if (!ok) IsLaunching = false;
        }
        catch (Exception ex)
        {
            HintText = $"启动失败：{ex.Message}";
            IsLaunching = false;
        }
        finally
        {
            // 启动流程异步进行，结束时复位（简单起见由日志事件观察）
            DispatcherTimer.RunOnce(() =>
            {
                if (GameLaunch.mcLaunchLoader.State != global::PCL.LoadState.Loading)
                    IsLaunching = false;
            }, TimeSpan.FromSeconds(5));
            _watchLaunchState();
        }
    }

    private async void _watchLaunchState()
    {
        await Task.Run(() =>
        {
            // 等待启动 loader 结束（成功/失败/中断都复位按钮）
            while (GameLaunch.mcLaunchLoader.State == global::PCL.LoadState.Loading)
                Thread.Sleep(500);
        });
        Dispatcher.UIThread.Post(() =>
        {
            IsLaunching = false;
            var state = GameLaunch.mcLaunchLoader.State;
            StatusText = state == global::PCL.LoadState.Finished ? "启动完成" : "就绪";
        });
    }

    public void KillGame()
    {
        try
        {
            GameLaunch.mcLaunchProcess?.Kill(true);
            HintText = "已请求终止游戏进程";
        }
        catch (Exception ex)
        {
            HintText = $"终止失败：{ex.Message}";
        }
    }

    private void _BuildDemoContent()
    {
        DemoCard.Content = new StackPanel
        {
            Spacing = 10,
            Margin = new Thickness(15, 44, 15, 18),
            Children =
            {
                new StackPanel
                {
                    Orientation = Orientation.Horizontal, Spacing = 10,
                    Children =
                    {
                        new Controls.MyButton { Text = "普通按钮" },
                        new Controls.MyButton { Text = "强调按钮", ColorType = Controls.MyButtonColorType.Highlight },
                        new Controls.MyButton { Text = "红色按钮", ColorType = Controls.MyButtonColorType.Red },
                    }
                },
                new TextBox { Watermark = "输入框（聚焦显示主题色边框）" },
                new CheckBox { Content = "复选框" },
                new StackPanel
                {
                    Orientation = Orientation.Horizontal, Spacing = 10,
                    Children =
                    {
                        new Controls.MyLoading(),
                        _HintButton("成功提示", PCL.Core.UI.Hints.HintKind.Success),
                        _HintButton("错误提示", PCL.Core.UI.Hints.HintKind.Error),
                        _MsgBoxButton(),
                    }
                },
            }
        };
    }

    private static Controls.MyButton _HintButton(string text, PCL.Core.UI.Hints.HintKind kind)
    {
        var button = new Controls.MyButton { Text = text };
        button.Click += (_, _) =>
            PCL.Core.UI.Hints.HintService.Show(new PCL.Core.UI.Hints.HintRequest
            {
                Message = kind == PCL.Core.UI.Hints.HintKind.Error ? "演示错误：这是 Error 提示条" : "演示成功：这是 Success 提示条",
                Kind = kind,
            });
        return button;
    }

    private static Controls.MyButton _MsgBoxButton()
    {
        var button = new Controls.MyButton { Text = "消息弹窗", ColorType = Controls.MyButtonColorType.Highlight };
        button.Click += async (_, _) =>
        {
            var choice = await Controls.MyMsgBox.ShowAsync(
                "这是 MyMsgBox 弹窗正文，支持多行文本与多个按钮。",
                "消息弹窗",
                ["确定", "取消"]);
            PCL.Core.UI.Hints.HintService.Show(new PCL.Core.UI.Hints.HintRequest
            {
                Message = $"你选择了：{choice}",
                Kind = PCL.Core.UI.Hints.HintKind.Success,
            });
        };
        return button;
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
