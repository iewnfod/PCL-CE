using Avalonia;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using PCL.Avalonia.Controls;
using PCL.Avalonia.Pages;
using PCL.Avalonia.Platform;
using PCL.Core.App.IoC;
using PCL.Core.App.Localization;

namespace PCL.Avalonia;

public partial class MainWindow : Window
{
    private PageLaunchLeft? _pageLaunchLeft;
    private PageLaunchRight? _pageLaunchRight;
    private PageSelectLeft? _pageSelectLeft;
    private PageSelectRight? _pageSelectRight;
    private Control? _pagePlaceholderLeft;
    private Control? _pagePlaceholderRight;

    private MainTab _tab = MainTab.Launch;
    private bool _launchSubSelect;
    private bool _autoLaunchDone;

    public MainWindow()
    {
        InitializeComponent();
        AppUi.Main = this;

        Loaded += (_, _) => HintPresenterBridge.Install(this);

        // 标题栏拖动：仅底层透明块（PanTitleDrag）响应，按钮区不受影响
        this.FindControl<Border>("PanTitleDrag")!.PointerPressed += (_, e) =>
        {
            if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
                BeginMoveDrag(e);
        };

        // 窗口控制
        this.FindControl<MyIconButton>("BtnTitleClose")!.Click += (_, _) => Close();
        this.FindControl<MyIconButton>("BtnTitleMin")!.Click += (_, _) =>
            WindowState = WindowState.Minimized;

        // 子页返回按钮 → 回启动主页
        this.FindControl<MyIconButton>("BtnTitleInner")!.Click += (_, _) => ShowLaunchHome();

        // 主导航
        _HookNavButton("BtnTitleSelect0", MainTab.Launch);
        _HookNavButton("BtnTitleSelect1", MainTab.Download);
        _HookNavButton("BtnTitleSelect2", MainTab.Setup);
        _HookNavButton("BtnTitleSelect3", MainTab.Tools);

        // 默认落在启动主页（PCL_NAV=select/download/setup/tools 直开对应页，供自动化验证）
        switch (Environment.GetEnvironmentVariable("PCL_NAV"))
        {
            case "select":
                ShowInstanceSelect();
                break;
            case "download":
            case "setup":
            case "tools":
                ShowTab(Environment.GetEnvironmentVariable("PCL_NAV") switch
                {
                    "download" => MainTab.Download,
                    "setup" => MainTab.Setup,
                    _ => MainTab.Tools,
                });
                break;
            default:
                ShowLaunchHome();
                break;
        }

        // 文件夹列表加载（对齐 WPF FormMain 构造器：McFolderListLoader.Start(0)）
        GameFolders.McFolderListLoader.Start(0);

        // 生命周期就绪后的自动任务
        Lifecycle.When(LifecycleState.Running, () => Dispatcher.UIThread.Post(_OnReady));
    }

    private void _HookNavButton(string name, MainTab tab)
    {
        var button = this.FindControl<MyRadioButton>(name)!;
        button.Check += (sender, _) =>
        {
            if (_syncingNav || !ReferenceEquals(sender, this.FindControl<MyRadioButton>($"BtnTitleSelect{(int)tab}"))) return;
            ShowTab(tab);
        };
    }

    private void _OnReady()
    {
        // 自动启动（--launch <实例名>）：用于端到端自动化验证（任务 4 验收链路）
        if (Program.AutoLaunchInstance is not { } name || _autoLaunchDone) return;
        _autoLaunchDone = true;
        _ = Task.Run(async () =>
        {
            // 等实例列表就绪
            for (var i = 0; i < 120; i++)
            {
                if (InstanceList.mcInstanceListLoader.State == LoadState.Finished) break;
                await Task.Delay(500);
            }

            McInstance? target = null;
            foreach (var list in InstanceList.mcInstanceList.Values)
            foreach (var instance in list)
                if (instance.Name == name)
                {
                    target = instance;
                    break;
                }

            if (target is null) return;
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                InstanceList.McMcInstanceSelected = target;
                _pageLaunchLeft?.RefreshState();
                _pageLaunchLeft?.Launch();
            });
        });
    }

    #region 页面栈（对应 WPF FormMain.PageChange 的 MVP 子集）

    public void ShowTab(MainTab tab)
    {
        _tab = tab;
        _SyncNavChecked();
        switch (tab)
        {
            case MainTab.Launch:
                if (_launchSubSelect) _ShowInstanceSelectCore();
                else _ShowLaunchHomeCore();
                break;
            case MainTab.Download:
            case MainTab.Setup:
            case MainTab.Tools:
                _ShowPlaceholderCore(tab);
                break;
        }
    }

    public void ShowLaunchHome()
    {
        _tab = MainTab.Launch;
        _launchSubSelect = false;
        _SyncNavChecked();
        _SyncTitleMode(null);
        _ShowLaunchHomeCore();
    }

    public void ShowInstanceSelect()
    {
        _tab = MainTab.Launch;
        _launchSubSelect = true;
        _SyncNavChecked();
        _SyncTitleMode("实例选择");
        _ShowInstanceSelectCore();
    }

    /// <summary>子页模式：标题栏 logo 与主导航整体替换为「← 标题」（对应 WPF PanTitleInner）。</summary>
    private void _SyncTitleMode(string? innerTitle)
    {
        var logo = this.FindControl<Grid>("PanTitleLeft")!;
        var select = this.FindControl<StackPanel>("PanTitleSelect")!;
        var inner = this.FindControl<Panel>("PanTitleInner")!;
        var label = this.FindControl<TextBlock>("LabTitleInner")!;
        if (innerTitle is null)
        {
            logo.IsVisible = true;
            select.IsVisible = true;
            inner.IsVisible = false;
        }
        else
        {
            label.Text = innerTitle;
            logo.IsVisible = false;
            select.IsVisible = false;
            inner.IsVisible = true;
        }
    }

    private void _ShowLaunchHomeCore()
    {
        _pageLaunchLeft ??= new PageLaunchLeft();
        _pageLaunchRight ??= new PageLaunchRight();
        _pageLaunchLeft.RefreshState();
        SetContent(_pageLaunchLeft, _pageLaunchRight);
    }

    private void _ShowInstanceSelectCore()
    {
        _pageSelectLeft ??= new PageSelectLeft();
        _pageSelectRight ??= new PageSelectRight();
        _pageSelectLeft.RefreshFolderList();
        _pageSelectRight.RefreshInstanceList();
        SetContent(_pageSelectLeft, _pageSelectRight);
    }

    private void _ShowPlaceholderCore(MainTab tab)
    {
        var title = tab switch
        {
            MainTab.Download => Lang.Text("Main.Tab.Download"),
            MainTab.Setup => Lang.Text("Main.Tab.Settings"),
            _ => Lang.Text("Main.Tab.Tools"),
        };

        // 左：分组导航列表（结构与文案对照 Windows 版）；右：占位卡片
        if (_pagePlaceholderLeft is null || _placeholderLeftTab != tab)
        {
            _placeholderLeftTab = tab;
            // 左栏贴左缘（选中竖线对齐实例选择页：列表项无边距，分组标题留边）
            var left = new StackPanel { Margin = new Thickness(0, 18, 5, 4) };

            void AddGroup(string? group)
            {
                if (group is null) return;
                left.Children.Add(new TextBlock
                {
                    Text = group,
                    Opacity = 0.6,
                    FontSize = 12,
                    Margin = new Thickness(13, 9, 0, 4),
                });
            }

            void AddItem(string itemTitle, string icon, bool @checked = false)
            {
                var item = new MyListItem
                {
                    IsScaleAnimationEnabled = false,
                    Type = MyListItemCheckType.Clickable,
                    Title = itemTitle,
                    Height = 34,
                    SvgIcon = icon,
                    LogoScale = 0.9,
                };
                // 点击切换选中态（页面内容为占位）
                item.Click += (sender, _) =>
                {
                    if (sender is MyListItem self && !self.Checked) self.Checked = true;
                };
                if (@checked) item.Checked = true;
                left.Children.Add(item);
            }

            switch (tab)
            {
                case MainTab.Download:
                    AddGroup(null);
                    AddItem("Minecraft", "lucide/box", true);
                    AddGroup("社区资源");
                    AddItem("模组", "lucide/grid-2x2");
                    AddItem("整合包", "lucide/package");
                    AddItem("数据包", "lucide/server");
                    AddItem("资源包", "lucide/file-archive");
                    AddItem("光影包", "lucide/sparkles");
                    AddItem("世界", "lucide/globe");
                    AddItem("收藏夹", "lucide/heart");
                    AddGroup("安装包");
                    AddItem("Minecraft", "lucide/box");
                    AddItem("OptiFine", "lucide/gauge");
                    AddItem("Forge", "lucide/anvil");
                    AddItem("NeoForge", "lucide/anvil");
                    break;
                case MainTab.Setup:
                    AddGroup("游戏");
                    AddItem("启动", "lucide/rocket", true);
                    AddItem("Java", "lucide/coffee");
                    AddItem("管理", "lucide/file-archive");
                    AddGroup("工具");
                    AddItem("联机", "lucide/user");
                    AddGroup("启动器");
                    AddItem("个性化", "lucide/palette");
                    AddItem("语言", "lucide/globe");
                    AddItem("杂项", "lucide/monitor-cog");
                    AddGroup("关于");
                    AddItem("软件信息", "lucide/info");
                    AddItem("软件更新", "lucide/refresh-cw");
                    AddItem("反馈", "lucide/message-circle");
                    AddItem("查看日志", "lucide/scroll-text");
                    break;
                default:
                    AddGroup("联机");
                    AddItem("大厅", "lucide/server", true);
                    AddItem("奇妙小工具", "lucide/sparkles");
                    AddItem("百宝箱", "lucide/package");
                    break;
            }

            _pagePlaceholderLeft = new ScrollViewer
            {
                Content = left,
                MinWidth = 250,
                MaxWidth = 300,
                VerticalAlignment = VerticalAlignment.Top,
            };
        }

        if (_pagePlaceholderRight is null)
        {
            var card = new MyCard
            {
                Margin = new Thickness(40),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Title = title,
            };
            card.Content = new StackPanel
            {
                Margin = new Thickness(20, 4, 20, 18),
                Children =
                {
                    new TextBlock
                    {
                        Text = "该页面将在后续版本提供（MVP 占位）",
                        HorizontalAlignment = HorizontalAlignment.Center,
                        Margin = new Thickness(0, 6, 0, 0),
                    },
                },
            };
            _pagePlaceholderRight = card;
        }

        SetContent(_pagePlaceholderLeft, _pagePlaceholderRight);
    }

    private MainTab _placeholderLeftTab;

    private void SetContent(Control left, Control right)
    {
        var panLeft = this.FindControl<ContentControl>("PanMainLeft")!;
        var panRight = this.FindControl<ContentControl>("PanMainRight")!;
        panLeft.Content = left;
        panRight.Content = right;
    }

    private void _SyncNavChecked()
    {
        if (_syncingNav) return;
        _syncingNav = true;
        try
        {
            var index = (int)_tab;
            for (var i = 0; i < 4; i++)
                this.FindControl<MyRadioButton>($"BtnTitleSelect{i}")!.Checked = i == index;
        }
        finally
        {
            _syncingNav = false;
        }
    }

    private bool _syncingNav;

    #endregion
}
