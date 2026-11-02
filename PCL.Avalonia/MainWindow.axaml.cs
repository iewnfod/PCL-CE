using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
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

    /// <summary>控件库演示卡片（任务 5 验收用，默认折叠；PCL_EXPAND_DEMO=1 时展开用于截图验收）。</summary>
    public Controls.MyCard DemoCard { get; } = new()
    {
        Title = "控件库演示（点击展开）",
        CanSwap = true,
        IsSwapped = Environment.GetEnvironmentVariable("PCL_EXPAND_DEMO") != "1",
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

        _ScheduleDemoDiagnostics();
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

    /// <summary>控件演示的自动化诊断：PCL_DIAG=1 输出一次快照；PCL_DIAG=2 每 0.5s 循环采样悬停状态。</summary>
    private void _ScheduleDemoDiagnostics()
    {
        var mode = Environment.GetEnvironmentVariable("PCL_DIAG");
        if (mode is not ("1" or "2" or "3")) return;

        T? Find<T>(Visual root) where T : Visual
        {
            if (root is T matched) return matched;
            foreach (var child in root.GetVisualChildren())
                if (Find<T>(child) is { } result)
                    return result;
            return null;
        }

        string Snapshot()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"[{DateTime.Now:HH:mm:ss.fff}]");
            foreach (var li in DemoCard.GetVisualDescendants().OfType<Controls.MyListItem>().Take(4))
            {
                var back = li.GetVisualDescendants().OfType<Border>()
                    .FirstOrDefault(b => b.Name == "PART_Back");
                sb.AppendLine($"  {li.Title}: over={li.IsPointerOver} fg={li.Foreground} " +
                              $"back={(back?.Background)} op={back?.Opacity} press={li.Classes.Contains(":pressed")}");
            }

            return sb.ToString();
        }

        if (mode == "1")
        {
            DispatcherTimer.RunOnce(() =>
            {
                var sb = new System.Text.StringBuilder();
                sb.AppendLine($"[{DateTime.Now:HH:mm:ss.fff}] 控件诊断");
                var app = global::Avalonia.Application.Current;
                foreach (var key in new[] { "ColorBrush1", "ColorBrush2", "ColorBrush3", "ColorBrushBg1", "ColorBrush6", "ColorBrushHalfWhite" })
                {
                    object? value = null;
                    var ok = app is not null && app.Resources.TryGetResource(key, app.ActualThemeVariant, out value);
                    sb.AppendLine($"  资源 {key}: {(ok ? value : "缺失")}");
                }

                var item = Find<Controls.MyListItem>(DemoCard);
                if (item is null)
                {
                    sb.AppendLine("  MyListItem: 未找到");
                }
                else
                {
                    var back = item.GetVisualDescendants().OfType<Border>()
                        .FirstOrDefault(b => b.Name == "PART_Back");
                    sb.AppendLine($"  MyListItem[0] Title={item.Title} Checked={item.Checked}");
                    sb.AppendLine($"    Foreground={item.Foreground}");
                    sb.AppendLine($"    伪类: {string.Join(",", item.Classes)}");
                    sb.AppendLine($"    PART_Back: Background={back?.Background} Opacity={back?.Opacity} CornerRadius={back?.CornerRadius}");
                }

                var hint = Find<Controls.MyHint>(DemoCard);
                sb.AppendLine($"  MyHint[0]: Background={hint?.Background} BorderBrush={hint?.BorderBrush} 可见={hint?.IsVisible}");

                var box = Find<Controls.MyCheckBox>(DemoCard);
                sb.AppendLine($"  MyCheckBox[0]: Checked={box?.Checked} BorderBrush={box?.BorderBrush}");

                File.WriteAllText("/tmp/opencode/pcl-diag.log", sb.ToString());
            }, TimeSpan.FromSeconds(4));
        }
        else if (mode == "3")
        {
            // 程序化触发 PointerEnter/Leave（Wayland 下无法用 xdotool 移动真实光标）：
            // 验证 :pointerover 样式链路（背景浮层 Opacity 0→1→0）
            DispatcherTimer.RunOnce(() =>
            {
                var sb = new System.Text.StringBuilder();
                var items = DemoCard.GetVisualDescendants().OfType<Controls.MyListItem>().ToList();
                var item = items.FirstOrDefault(i => i.Buttons is { Count: > 0 });
                if (item is null)
                {
                    File.WriteAllText("/tmp/opencode/pcl-diag3.log", "未找到带按钮的列表项");
                    return;
                }

                Border Back() => item.GetVisualDescendants().OfType<Border>()
                    .First(b => b.Name == "PART_Back");
                Panel Buttons() => item.GetVisualDescendants().OfType<Panel>()
                    .First(b => b.Name == "PART_Buttons");

                sb.AppendLine($"目标: {item.Title}");
                sb.AppendLine($"初始: over={item.IsPointerOver} backOp={Back().Opacity} buttonsOp={Buttons().Opacity}");

                var pointer = new global::Avalonia.Input.Pointer(Pointer.GetNextFreeId(), PointerType.Mouse, true);
                var position = new Point(item.Bounds.Width / 2, item.Bounds.Height / 2);
                var props = new PointerPointProperties();
                item.RaiseEvent(new PointerEventArgs(InputElement.PointerEnteredEvent, item, pointer, item,
                    position, (ulong)Environment.TickCount64,
                    props, global::Avalonia.Input.KeyModifiers.None));

                void Sample(string tag) => sb.AppendLine(
                    $"{tag}: over={item.IsPointerOver} backOp={Back().Opacity} backColor={Back().Background} buttonsOp={Buttons().Opacity}");

                // 等样式与过渡动画应用后再采样
                var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(400) };
                var phase = 0;
                timer.Tick += (_, _) =>
                {
                    phase++;
                    switch (phase)
                    {
                        case 1:
                            Sample("enter后");
                            item.RaiseEvent(new PointerEventArgs(InputElement.PointerExitedEvent, item, pointer, item,
                                position, (ulong)Environment.TickCount64,
                                props, global::Avalonia.Input.KeyModifiers.None));
                            break;
                        case 2:
                            Sample("leave后");
                            File.WriteAllText("/tmp/opencode/pcl-diag3.log", sb.ToString());
                            timer.Stop();
                            break;
                    }
                };
                timer.Start();
            }, TimeSpan.FromSeconds(4));
        }
        else
        {
            // 循环采样：与外部 xdotool 联动验证悬停状态机
            var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
            var sw = new System.IO.StreamWriter("/tmp/opencode/pcl-diag2.log", append: false);
            timer.Tick += (_, _) => sw.Write(Snapshot());
            timer.Start();
        }
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
            HintText = $"未找到实例：请将版本文件夹放入 {Path.Join(GameFolders.mcFolderSelected, "versions")} 后点「刷新」";
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
        // 两列布局：避免内容高度超出窗口可视区被截断（无滚动条即无遮挡）
        var left = new StackPanel { Spacing = 8 };
        foreach (var item in _LeftDemoItems()) left.Children.Add(item);
        var right = new StackPanel { Spacing = 4, Margin = new Thickness(14, 0, 0, 0) };
        foreach (var item in _RightDemoItems()) right.Children.Add(item);
        Grid.SetColumn(right, 1);
        DemoCard.Content = new Grid
        {
            Margin = new Thickness(15, 44, 15, 18),
            ColumnDefinitions = new ColumnDefinitions("1.15*,1*"),
            Children = { left, right }
        };
    }

    private static List<Control> _LeftDemoItems()
    {
        return
        [
            new StackPanel
            {
                Orientation = Orientation.Horizontal, Spacing = 8,
                Children =
                {
                    new Controls.MyButton { Text = "普通" },
                    new Controls.MyButton { Text = "强调", ColorType = Controls.MyButtonColorType.Highlight },
                    new Controls.MyButton { Text = "红色", ColorType = Controls.MyButtonColorType.Red },
                }
            },
            new TextBox { Watermark = "输入框" },
            new StackPanel
            {
                Orientation = Orientation.Horizontal, Spacing = 8,
                Children =
                {
                    new Controls.MyLoading(),
                    _HintButton("成功提示", PCL.Core.UI.Hints.HintKind.Success),
                    _MsgBoxButton(),
                }
            },
            _BuildIconButtonDemo(),
            _BuildCheckBoxDemo(),
            new Controls.MyHint { Theme = Controls.MyHintTheme.Red, Text = "Red 提示：实例启动失败，请检查 Java 路径。" },
        ];
    }

    private static List<Control> _RightDemoItems()
    {
        return
        [
            _BuildListItemDemo(),
            new Controls.MyHint { Theme = Controls.MyHintTheme.Blue, Text = "Blue：已识别 3 个 Java。" },
            new Controls.MyHint { Theme = Controls.MyHintTheme.Yellow, Text = "Yellow：内存分配超过建议值。" },
        ];
    }

    /// <summary>MyIconButton 演示：四种主题 + 两种图标来源（SvgIcon / Logo 矢量路径）。</summary>
    private static Control _BuildIconButtonDemo()
    {
        var logoButton = new Controls.MyIconButton
        {
            Logo = "M0,6L1.5,4.5 4.5,7.5 10.5,1.5 12,3 4.5,10.5 0,6z", Width = 28, Height = 28,
        };
        ToolTip.SetTip(logoButton, "Logo 矢量路径按钮");
        return new StackPanel
        {
            Orientation = Orientation.Horizontal, Spacing = 12,
            Children =
            {
                new Controls.MyIconButton { SvgIcon = "lucide/x", Width = 28, Height = 28 },
                new Controls.MyIconButton { SvgIcon = "lucide/refresh-cw", Theme = Controls.MyIconButtonTheme.Red, Width = 28, Height = 28 },
                new Controls.MyIconButton { SvgIcon = "lucide/settings", Theme = Controls.MyIconButtonTheme.Black, Width = 28, Height = 28 },
                new Controls.MyIconButton
                {
                    SvgIcon = "lucide/circle-check", Theme = Controls.MyIconButtonTheme.Custom, Width = 28, Height = 28,
                    Foreground = Brushes.OrangeRed
                },
                logoButton,
            }
        };
    }

    /// <summary>MyListItem 演示：单选组（Logo/Info/Tags/右侧按钮）+ 复选项。</summary>
    private static Control _BuildListItemDemo()
    {
        var list = new StackPanel { Spacing = 2 };

        void AddRadio(string title, string? info, string? svgIcon = null, bool @checked = false)
        {
            list.Children.Add(new Controls.MyListItem
            {
                Title = title,
                Info = info,
                SvgIcon = svgIcon,
                Type = Controls.MyListItemCheckType.RadioBox,
                Checked = @checked,
            });
        }

        AddRadio("1.20.4 原版实例", "上次游玩：昨天 21:36", "lucide/anvil", true);
        AddRadio("Forge 1.20.1 工业时代", "上次游玩：3 天前 · 47 个 Mod", "lucide/arrow-right");
        AddRadio("Fabric 1.21 轻量整合", "上次游玩：两周前", "lucide/globe");
        list.Children.Add(new Controls.MyListItem
        {
            Title = "允许后台运行",
            Info = "游戏启动后不退出启动器",
            SvgIcon = "lucide/settings",
            Type = Controls.MyListItemCheckType.CheckBox,
            Buttons = [new Controls.MyIconButton { SvgIcon = "lucide/refresh-cw" }],
        });
        return list;
    }

    /// <summary>MyCheckBox 演示：两态 / 三态。</summary>
    private static Control _BuildCheckBoxDemo()
    {
        var panel = new StackPanel
        {
            Orientation = Orientation.Horizontal, Spacing = 16,
            Children =
            {
                new Controls.MyCheckBox { Text = "两态复选框", Checked = true },
                new Controls.MyCheckBox { Text = "三态复选框", IsThreeState = true, Checked = null },
                new Controls.MyCheckBox { Text = "禁用态", IsEnabled = false, Checked = true },
            }
        };
        return panel;
    }

    /// <summary>MyHint 演示并入左右两列（_LeftDemoItems/_RightDemoItems）。</summary>

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
