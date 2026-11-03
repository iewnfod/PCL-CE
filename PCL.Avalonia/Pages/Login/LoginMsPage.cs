using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using PCL.Avalonia.Controls;
using PCL.Core.App.Localization;
using PCL.Core.Logging;
using PCL.Core.Minecraft.Profile;
using PCL.Core.UI.Hints;

namespace PCL.Avalonia.Pages.Login;

/// <summary>
///     微软登录页（复刻 WPF PageLoginMs）：人像图标 + 「开始正版验证」按钮（进度实时刷新）+
///     购买正版 / 前往官网链接 + 返回。登录走 mcLoginMsLoader（设备码弹窗由 AvaloniaProfileUi 接管）。
/// </summary>
public class LoginMsPage : Grid
{
    private readonly LoginPageHost _host;
    private readonly MyButton _btnLogin = new()
    {
        Text = Lang.Text("Launch.Account.Microsoft.Start"),
        ColorType = MyButtonColorType.Highlight,
        Height = 28,
        Padding = new Thickness(16, 0),
        HorizontalAlignment = HorizontalAlignment.Center,
    };
    private readonly MyButton _btnBack = new()
    {
        Text = Lang.Text("Launch.Account.Back"),
        Height = 28,
        Padding = new Thickness(14, 0),
        HorizontalAlignment = HorizontalAlignment.Center,
        Margin = new Thickness(0, 24, 0, 0),
    };

    public LoginMsPage(LoginPageHost host)
    {
        _host = host;
        VerticalAlignment = VerticalAlignment.Center;

        // 人像图标（复刻 WPF Path Data 的视觉：ColorBrush2、Opacity 0.7、40px）
        var avatarIcon = new SvgIcon
        {
            Icon = "lucide/user",
            Width = 40,
            Height = 40,
        };
        avatarIcon.SetDynamic(SvgIcon.IconBrushProperty, "ColorBrush2");
        var avatar = new Border
        {
            Width = 40,
            Height = 40,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(4, 0, 0, 22),
            Child = avatarIcon,
        };
        avatar.Opacity = 0.7;

        // 购买正版 / 前往官网（» 文字链接，Opacity 0.3）
        TextBlock Link(string text, string url)
        {
            var link = new TextBlock
            {
                Text = text,
                FontSize = 12.5,
                Cursor = new global::Avalonia.Input.Cursor(global::Avalonia.Input.StandardCursorType.Hand),
            };
            link.SetDynamic(TextBlock.ForegroundProperty, "ColorBrush3");
            link.PointerPressed += (_, _) => PCL.LauncherUi.Interaction?.OpenWebsite(url);
            return link;
        }

        var links = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Center,
            Spacing = 30,
            Margin = new Thickness(2, 26, 2, 0),
            Opacity = 0.45,
            Children =
            {
                Link(Lang.Text("Launch.Account.Microsoft.Purchase"),
                    "https://www.xbox.com/zh-cn/games/store/minecraft-java-bedrock-edition-for-pc/9nxp44l49shj"),
                Link(Lang.Text("Launch.Account.Microsoft.Website"), "https://www.minecraft.net/zh-hans"),
            },
        };

        Children.Add(new StackPanel
        {
            Children = { avatar, _btnLogin, links, _btnBack },
        });

        _btnLogin.Click += (_, _) => _ = LoginAsync();
        _btnBack.Click += (_, _) => Back();
    }

    private void Back()
    {
        ProfileService.IsCreatingProfile = false;
        _host.RefreshPage();
    }

    /// <summary>微软登录（复刻 PageLoginMsViewModel.Login，含进度刷新 / 取消恢复 / 错误分支）。</summary>
    private async Task LoginAsync()
    {
        _btnLogin.IsEnabled = false;
        _btnBack.IsVisible = false;
        _btnLogin.Text = Lang.Number(0d, "P0");

        var previousProfile = ProfileService.Current;
        var loginSucceeded = false;
        try
        {
            ProfileService.Select(null);
            GameLaunch.mcLoginMsLoader.Start(new GameLaunch.McLoginMs(), true);
            while (GameLaunch.mcLoginMsLoader.State == LoadState.Loading)
            {
                var progress = GameLaunch.mcLoginMsLoader.Progress;
                Dispatcher.UIThread.Post(() => _btnLogin.Text = Lang.Number(progress, "P0"));
                await Task.Delay(50);
            }

            switch (GameLaunch.mcLoginMsLoader.State)
            {
                case LoadState.Finished:
                    loginSucceeded = true;
                    break;
                case LoadState.Aborted:
                    throw new ThreadInterruptedException();
                case LoadState.Failed when GameLaunch.mcLoginMsLoader.Error is null:
                    throw new InvalidOperationException(Lang.Text("Launch.Account.Microsoft.Error.Unknown"));
                default:
                    throw GameLaunch.mcLoginMsLoader.Error!;
            }
        }
        catch (ThreadInterruptedException)
        {
            if (!loginSucceeded && previousProfile is not null) ProfileService.Select(previousProfile);
            HintService.Show(Hints.Info(Lang.Text("Launch.Account.LoginCancelled")));
        }
        catch (Exception ex)
        {
            if (!loginSucceeded && previousProfile is not null) ProfileService.Select(previousProfile);
            if (ex.Message == "$$")
            {
                // 已由 HandleMicrosoftXstsError 等弹窗处理
            }
            else if (ex.Message.StartsWith("$"))
            {
                HintService.Show(Hints.Error(Lang.Text(
                    "Launch.Account.Microsoft.LoginFailed.WithDetail", ex.Message.TrimStart('$'))));
            }
            else
            {
                LogWrapper.Error(ex, "Profile", "正版登录尝试失败");
                HintService.Show(Hints.Error(Lang.Text("Launch.Account.Microsoft.LoginFailed.Title")));
            }
        }
        finally
        {
            ProfileService.IsCreatingProfile = false;
            Dispatcher.UIThread.Post(() =>
            {
                _btnLogin.IsEnabled = true;
                _btnBack.IsVisible = true;
                _btnLogin.Text = Lang.Text("Launch.Account.Login");
            });

            // 成功：刷新登录区与启动按钮；失败：回到档案列表
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                _host.RefreshPage();
                AppUi.Main?.NotifyProfileChanged();
            });
        }
    }
}
