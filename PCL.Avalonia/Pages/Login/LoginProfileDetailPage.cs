using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using PCL.Avalonia.Controls;
using PCL.Avalonia.Platform;
using PCL.Core.App.Localization;
using PCL.Core.Logging;
using PCL.Core.Minecraft.Profile;
using PCL.Core.Minecraft.Profile.Models;
using PCL.Core.UI.Hints;

namespace PCL.Avalonia.Pages.Login;

/// <summary>
///     当前档案详情页（复刻 WPF PageLoginProfileSkin 的 MVP 子集）：
///     皮肤头像（在线合成，失败回退人像图标）+ 玩家名 + 类型 + 操作按钮（刷新皮肤 / 切换档案 / 删除）。
/// </summary>
public class LoginProfileDetailPage : Grid
{
    private readonly LoginPageHost _host;
    private readonly Border _avatar = new()
    {
        Width = 84,
        Height = 84,
        CornerRadius = new CornerRadius(6),
        HorizontalAlignment = HorizontalAlignment.Center,
        ClipToBounds = true,
    };
    private readonly TextBlock _labName = new()
    {
        FontSize = 16,
        FontWeight = FontWeight.Bold,
        HorizontalAlignment = HorizontalAlignment.Center,
        Margin = new Thickness(8, 10, 8, 0),
        TextTrimming = TextTrimming.CharacterEllipsis,
    };
    private readonly TextBlock _labType = new()
    {
        FontSize = 12,
        HorizontalAlignment = HorizontalAlignment.Center,
        Margin = new Thickness(8, 4, 8, 5),
        TextTrimming = TextTrimming.CharacterEllipsis,
    };

    public LoginProfileDetailPage(LoginPageHost host)
    {
        _host = host;
        VerticalAlignment = VerticalAlignment.Center;

        _avatar.SetDynamic(Border.BackgroundProperty, "ColorBrush6");
        _avatar.Child = new SvgIcon
        {
            Icon = "lucide/user",
            Width = 44,
            Height = 44,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        _avatar.Child.SetDynamic(SvgIcon.IconBrushProperty, "ColorBrush2");

        _labType.SetDynamic(TextBlock.ForegroundProperty, "ColorBrushGray4");

        var btnRefresh = new MyIconButton
        {
            SvgIcon = "lucide/refresh-cw",
            Height = 24,
            Width = 30,
            LogoScale = 0.9,
        };
        btnRefresh.Click += (_, _) => RefreshSkin();
        var btnSwitch = new MyIconButton
        {
            SvgIcon = "lucide/arrow-right-left",
            Height = 24,
            Width = 30,
            LogoScale = 0.9,
        };
        btnSwitch.Click += (_, _) => ChangeProfile();
        var btnDelete = new MyIconButton
        {
            SvgIcon = "lucide/trash-2",
            Height = 24,
            Width = 30,
            LogoScale = 0.9,
        };
        btnDelete.Click += (_, _) => _ = DeleteProfileAsync();

        var card = new MyCard
        {
            CornerRadius = new CornerRadius(5),
            Margin = new Thickness(0, 8, 0, 0),
            HorizontalAlignment = HorizontalAlignment.Center,
            Content = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(10, 3, 9, 3),
                Children = { btnRefresh, btnSwitch, btnDelete },
            },
        };

        Children.Add(new StackPanel
        {
            Children = { _avatar, _labName, _labType, card },
        });

        Reload();
    }

    /// <summary>刷新显示（复刻 PageLoginProfileSkin.Reload 的 MVP 子集）。</summary>
    public void Reload()
    {
        var profile = ProfileService.Current;
        if (profile is null) return;

        LogWrapper.Debug("Profile", "刷新档案界面");
        _labName.Text = profile.UserName;
        _labType.Text = AvaloniaProfileUi.GetProfileInfo(profile);
        _LoadAvatar(profile);
    }

    private void _LoadAvatar(McProfile profile)
    {
        var captured = profile;
        _ = Task.Run(() =>
        {
            var path = McSkinHelper.TryGetAvatarPath(captured);
            Dispatcher.UIThread.Post(() =>
            {
                if (ProfileService.Current?.ProfileId != captured.ProfileId) return;
                if (path is not null)
                {
                    using var stream = File.OpenRead(path);
                    _avatar.Background = null;
                    _avatar.Child = new Image
                    {
                        Source = new Bitmap(stream),
                        Stretch = Stretch.UniformToFill,
                    };
                }
                else
                {
                    // 失败回退：人像图标
                    _avatar.SetDynamic(Border.BackgroundProperty, "ColorBrush6");
                    _avatar.Child = new SvgIcon
                    {
                        Icon = "lucide/user",
                        Width = 44,
                        Height = 44,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center,
                    };
                    _avatar.Child.SetDynamic(SvgIcon.IconBrushProperty, "ColorBrush2");
                }
            });
        });
    }

    /// <summary>刷新皮肤：清空地址缓存与 SkinHeadId 后重新拉取（复刻 Skin.RefreshClick 语义）。</summary>
    private void RefreshSkin()
    {
        var profile = ProfileService.Current;
        if (profile is null) return;
        try
        {
            var cachePath = System.IO.Path.Combine(LauncherPaths.Temp, "Cache", "Skin", "IndexMs.ini");
            if (!string.IsNullOrWhiteSpace(profile.Uuid))
                LauncherCommon.WriteIni(cachePath, profile.Uuid, "");
            var updated = profile.Clone();
            updated.SkinHeadId = "";
            ProfileService.Update(profile, updated);
            ProfileService.Select(updated);
            HintService.Show(Hints.Info("正在刷新皮肤…"));
            Reload();
        }
        catch (Exception ex)
        {
            LogWrapper.Warn("Profile", "刷新皮肤失败：" + ex.Message);
        }
    }

    /// <summary>切换档案：清空当前选择，回到档案列表（复刻 ChangeProfile）。</summary>
    private void ChangeProfile()
    {
        ProfileService.Select(null);
        _host.RefreshPage();
        AppUi.Main?.NotifyProfileChanged();
    }

    private async Task DeleteProfileAsync()
    {
        var profile = ProfileService.Current;
        if (profile is null) return;
        if (await MyMsgBox.ShowAsync(
                Lang.Text("Launch.Account.Profile.DeleteConfirm.Message"),
                Lang.Text("Launch.Account.Profile.DeleteConfirm.Title"),
                [Lang.Text("Common.Action.Continue"), Lang.Text("Common.Action.Cancel")],
                isWarn: true) != 1)
            return;

        ProfileService.Remove(profile);
        HintService.Show(Hints.Success(Lang.Text("Launch.Account.Profile.Deleted")));
        _host.RefreshPage();
        AppUi.Main?.NotifyProfileChanged();
    }
}
