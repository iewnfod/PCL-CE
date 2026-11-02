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
using PCL.Core.App.Localization;

namespace PCL.Avalonia.Pages;

/// <summary>
///     启动主页左侧页（复刻 WPF PageLaunchLeft）：登录区（头像 + 玩家名）居中、
///     启动按钮（高 54、Highlight、下缘版本名 11px Gray3）、底部「实例选择 / 实例设置」按钮（高 35）。
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

        // 登录区（Row 1，居中）：头像 + 玩家名 + 档案类型
        SetRow(_panLogin, 1);
        SetColumnSpan(_panLogin, 5);
        _panLogin.Margin = new Thickness(20, 0);
        _panLogin.VerticalAlignment = VerticalAlignment.Center;
        _panLogin.Child = _BuildLoginPanel();
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
    }

    /// <summary>登录区：圆形头像占位（lucide/user）+ 玩家名 + 档案说明。</summary>
    private Control _BuildLoginPanel()
    {
        string userName;
        string profileDesc;
        try
        {
            var profile = PCL.Core.Minecraft.Profile.ProfileService.Current;
            userName = profile?.UserName ?? "Player";
            profileDesc = profile?.ProfileType == PCL.Core.Minecraft.Profile.Models.ProfileType.Microsoft
                ? "微软账户"
                : "离线账户";
        }
        catch
        {
            userName = "Player";
            profileDesc = "离线账户";
        }

        var avatar = new Border
        {
            Width = 84,
            Height = 84,
            CornerRadius = new CornerRadius(42),
            HorizontalAlignment = HorizontalAlignment.Center,
            Child = new SvgIcon
            {
                Icon = "lucide/user",
                Width = 44,
                Height = 44,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            },
        };
        avatar.SetDynamic(Border.BackgroundProperty, "ColorBrush6");
        avatar.Child.SetDynamic(SvgIcon.IconBrushProperty, "ColorBrush2");

        return new StackPanel
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            Spacing = 8,
            Children =
            {
                avatar,
                new TextBlock
                {
                    Text = userName,
                    FontSize = 19,
                    FontWeight = FontWeight.Bold,
                    HorizontalAlignment = HorizontalAlignment.Center,
                },
                new TextBlock
                {
                    Text = profileDesc,
                    FontSize = 12,
                    Opacity = 0.6,
                    HorizontalAlignment = HorizontalAlignment.Center,
                },
                new MyButton
                {
                    Text = "切换账户",
                    Height = 30,
                    Margin = new Thickness(0, 6, 0, 0),
                    HorizontalAlignment = HorizontalAlignment.Center,
                },
            },
        };
        // 切换账户：挂点击（登录功能在任务 9，先给出可见反馈）
        if (_panLogin.Child is StackPanel loginStack &&
            loginStack.Children.LastOrDefault() is MyButton btnAccount)
            btnAccount.Click += (_, _) =>
                global::PCL.Core.UI.Hints.HintService.Show(new()
                {
                    Message = "账户切换与登录将在后续版本提供（当前使用离线档案 Player）",
                    Kind = global::PCL.Core.UI.Hints.HintKind.Info,
                });
    }

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
            _btnLaunch.IsEnabled = true;
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
