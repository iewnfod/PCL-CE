using Avalonia;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using PCL.Avalonia.Controls;
using PCL.Avalonia.Platform;
using PCL.Avalonia.Pages.Login;
using PCL.Core.App.Localization;

namespace PCL.Avalonia.Pages;

/// <summary>
///     启动主页左侧页（复刻 WPF PageLaunchLeft）：登录区（任务 9：LoginPageHost 子页体系——
///     档案列表 / 档案详情 / 微软登录 / 离线登录）、启动按钮（高 54、Highlight、下缘版本名 11px Gray3）、
///     底部「实例选择 / 实例设置」按钮（高 35）。
/// </summary>
public class PageLaunchLeft : Grid
{
    private readonly MyButton _btnLaunch = new()
    {
        Height = 54,
        Margin = new Thickness(20, 0),
        ColorType = MyButtonColorType.Highlight,
        FontSize = 14,
        Padding = new Thickness(30, 0, 30, 15),
    };

    private readonly TextBlock _labVersion = new()
    {
        Margin = new Thickness(35, 0, 35, 10),
        VerticalAlignment = VerticalAlignment.Bottom,
        HorizontalAlignment = HorizontalAlignment.Center,
        TextTrimming = TextTrimming.CharacterEllipsis,
        FontSize = 11,
        IsHitTestVisible = false,
    };

    private readonly MyButton _btnInstance = new() { Height = 35, Margin = new Thickness(0, 10, 10, 0) };
    private readonly MyButton _btnMore = new() { Height = 35, Margin = new Thickness(0, 10, 10, 0) };
    private readonly Border _panLogin = new();
    private readonly LoginPageHost _loginHost = new();
    private readonly ProgressBar _progress = new()
    {
        Height = 4, Margin = new Thickness(30, 12, 30, 0), Minimum = 0, Maximum = 1,
        IsVisible = false, IsHitTestVisible = false,
    };

    private bool _isLaunching;

    public PageLaunchLeft()
    {
        Width = 300;
        RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        RowDefinitions.Add(new RowDefinition(GridLength.Star));
        RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        RowDefinitions.Add(new RowDefinition(new GridLength(20)));
        ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        ColumnDefinitions.Add(new ColumnDefinition(new GridLength(20)));
        ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        ColumnDefinitions.Add(new ColumnDefinition(new GridLength(10)));

        // 登录区（Row 1，居中）：登录子页体系（档案详情 / 列表 / 登录页）
        SetRow(_panLogin, 1);
        SetColumnSpan(_panLogin, 5);
        _panLogin.Margin = new Thickness(20, 0);
        _panLogin.VerticalAlignment = VerticalAlignment.Center;
        _panLogin.Child = _loginHost;
        Children.Add(_panLogin);

        // 启动按钮（Row 2）+ 版本名覆盖
        var launchPanel = new Grid();
        launchPanel.Children.Add(_btnLaunch);
        launchPanel.Children.Add(_labVersion);
        SetRow(launchPanel, 2);
        SetColumnSpan(launchPanel, 5);
        Children.Add(launchPanel);

        // 进度条
        SetRow(_progress, 3);
        SetColumnSpan(_progress, 4);
        Children.Add(_progress);

        // 底部按钮（Row 3）
        _btnInstance.Text = Lang.Text("Launch.Home.SelectInstance");
        _btnInstance.Click += (_, _) => AppUi.NavigateToInstanceSelect();
        SetRow(_btnInstance, 3);
        SetColumn(_btnInstance, 2);
        Children.Add(_btnInstance);

        _btnMore.Text = Lang.Text("Launch.Home.InstanceSettings");
        _btnMore.Click += (_, _) =>
            global::PCL.Core.UI.Hints.HintService.Show(new()
            {
                Message = "实例设置将在后续版本提供（MVP 占位）",
                Kind = global::PCL.Core.UI.Hints.HintKind.Info,
            });
        SetRow(_btnMore, 3);
        SetColumn(_btnMore, 3);
        Children.Add(_btnMore);

        _btnLaunch.Click += (_, _) =>
        {
            if (InstanceList.McMcInstanceSelected is null)
            {
                // 无实例：与 WPF 一致跳转下载页
                AppUi.NavigateToTab(MainTab.Download);
                return;
            }

            Launch();
        };

        UiBridgeHub.LaunchProgressChanged += p => Dispatcher.UIThread.Post(() => _progress.Value = p);
        RefreshState();
        RefreshLogin();

        // 档案服务后台加载完成后刷新登录区（首次启动时 Load 在生命周期线程异步执行）
        _ = Task.Run(async () =>
        {
            for (var i = 0; i < 40 && !PCL.Core.Minecraft.Profile.ProfileService.IsLoaded; i++)
                await Task.Delay(100);
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                RefreshLogin();
                RefreshState();
            });
        });
    }

    /// <summary>刷新登录区子页（档案状态变化时调用；对齐 WPF RefreshPage）。</summary>
    public void RefreshLogin() => _loginHost.RefreshPage();

    /// <summary>刷新启动按钮与版本名状态。</summary>
    public void RefreshState()
    {
        if (_isLaunching)
        {
            _btnLaunch.Text = Lang.Text("Launch.Home.Button.Loading");
            _btnLaunch.IsEnabled = false;
            _progress.IsVisible = true;
            return;
        }

        _progress.IsVisible = false;
        _progress.Value = 0;
        var instance = InstanceList.McMcInstanceSelected;
        var hasProfile = false;
        try { hasProfile = PCL.Core.Minecraft.Profile.ProfileService.Current is not null; }
        catch { /* 档案服务未就绪 */ }

        if (instance is null)
        {
            // 无实例：与 WPF 版一致显示「下载游戏」+ 副标题（点击提示下载页占位）
            _btnLaunch.Text = Lang.Text("Launch.Home.Button.Download");
            _btnLaunch.IsEnabled = true;
            _labVersion.Text = Lang.Text("Launch.Home.Instance.NotFound");
        }
        else
        {
            _btnLaunch.Text = Lang.Text("Launch.Home.Button.Launch");
            // 对齐 WPF RefreshButtonsUI case 3：无档案时禁用启动（登录区引导创建档案）
            _btnLaunch.IsEnabled = hasProfile;
            _labVersion.Text = instance.Name;
        }
    }

    /// <summary>启动游戏（迁移自任务 4 的 MainWindowViewModel.Launch）。</summary>
    public void Launch()
    {
        if (_isLaunching) return;
        var instance = InstanceList.McMcInstanceSelected;
        if (instance is null)
        {
            global::PCL.Core.UI.Hints.HintService.Show(new()
            {
                Message = Lang.Text("Launch.Home.Instance.NotFound"),
                Kind = global::PCL.Core.UI.Hints.HintKind.Error,
            });
            return;
        }

        try
        {
            var fresh = new McInstance(instance.PathInstance);
            fresh.Load();
            if (fresh.state == McInstanceState.Error)
            {
                global::PCL.Core.UI.Hints.HintService.Show(new()
                {
                    Message = $"实例不可用：{fresh.Desc}",
                    Kind = global::PCL.Core.UI.Hints.HintKind.Error,
                });
                return;
            }

            _isLaunching = true;
            RefreshState();

            var ok = GameLaunch.McLaunchStart(new GameLaunch.McLaunchOptions { instance = fresh });
            if (!ok)
            {
                _isLaunching = false;
                RefreshState();
                return;
            }

            _ = Task.Run(async () =>
            {
                // 等待启动 loader 结束（成功/失败/中断都复位按钮）
                while (GameLaunch.mcLaunchLoader.State == LoadState.Loading)
                    await Task.Delay(500);
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    _isLaunching = false;
                    RefreshState();
                });
            });
        }
        catch (Exception ex)
        {
            _isLaunching = false;
            RefreshState();
            global::PCL.Core.UI.Hints.HintService.Show(new()
            {
                Message = $"启动失败：{ex.Message}",
                Kind = global::PCL.Core.UI.Hints.HintKind.Error,
            });
        }
    }
}
