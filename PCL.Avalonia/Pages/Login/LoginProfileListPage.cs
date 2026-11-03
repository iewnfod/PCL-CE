using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
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
///     档案列表页（复刻 WPF PageLoginProfile）：提示条 + 档案列表（头像/名称/类型，
///     点击选择；悬停按钮：复制 UUID / 删除）+ 底部「新建档案」按钮。
/// </summary>
public class LoginProfileListPage : Grid
{
    private readonly LoginPageHost _host;
    private readonly StackPanel _listPanel = new();
    private readonly MyHint _hintEmpty = new() { Theme = MyHintTheme.Blue, HasBorder = true };
    private readonly MyHint _hintSelect = new() { Theme = MyHintTheme.Blue };

    public LoginProfileListPage(LoginPageHost host)
    {
        _host = host;
        RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        RowDefinitions.Add(new RowDefinition(GridLength.Auto));

        _hintEmpty.Text = Lang.Text("Launch.Account.Profile.CreateAndSelectHint");
        Grid.SetRow(_hintEmpty, 0);
        _hintEmpty.Margin = new Thickness(0, 10, 0, 0);
        Children.Add(_hintEmpty);

        _hintSelect.Text = Lang.Text("Launch.Account.Profile.SelectHint");
        Grid.SetRow(_hintSelect, 1);
        _hintSelect.Margin = new Thickness(0, 10, 0, 0);
        Children.Add(_hintSelect);

        var scroll = new ScrollViewer
        {
            Content = _listPanel,
            MaxHeight = 300,
            Margin = new Thickness(10, 0, 10, 0),
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
        };
        Grid.SetRow(scroll, 2);
        Children.Add(scroll);

        // 底部新建按钮（对齐 WPF：小卡内 user-plus 图标按钮）
        var btnNew = new MyIconButton
        {
            SvgIcon = "lucide/user-plus",
            Height = 24,
            Width = 30,
            LogoScale = 1.08,
        };
        btnNew.Click += (_, _) => _ = CreateProfileAsync();
        var card = new MyCard
        {
            CornerRadius = new CornerRadius(5),
            Margin = new Thickness(0, 10, 4, 0),
            HorizontalAlignment = HorizontalAlignment.Center,
            Content = btnNew,
        };
        Grid.SetRow(card, 3);
        Children.Add(card);

        Reload();
    }

    /// <summary>重载档案列表（复刻 PageLoginProfileViewModel.ReloadAsync）。</summary>
    public void Reload()
    {
        _listPanel.Children.Clear();
        var profiles = ProfileService.Profiles;
        var isEmpty = profiles.Count == 0;
        _hintEmpty.IsVisible = isEmpty;
        _hintSelect.IsVisible = !isEmpty;

        foreach (var profile in profiles)
        {
            var item = new MyListItem
            {
                Title = profile.UserName,
                Info = AvaloniaProfileUi.GetProfileInfo(profile),
                Type = MyListItemCheckType.Clickable,
                SvgIcon = "lucide/user",
                LogoScale = 0.72,
                Margin = new Thickness(8, 2, 10, 2),
                Tag = profile,
            };
            item.Click += (_, _) => SelectProfile(profile);

            // 悬停按钮：复制 UUID（正版）/ 删除
            var btnCopy = new MyIconButton
            {
                SvgIcon = "lucide/copy",
                Height = 22,
                Width = 28,
                LogoScale = 0.85,
                Tag = profile,
            };
            btnCopy.Click += (_, _) =>
            {
                _ = AppUi.Main?.Clipboard?.SetTextAsync(profile.Uuid);
                HintService.Show(Hints.Success("UUID 已复制"));
            };
            var btnDelete = new MyIconButton
            {
                SvgIcon = "lucide/trash-2",
                Height = 22,
                Width = 28,
                LogoScale = 0.85,
                Tag = profile,
            };
            btnDelete.Click += (_, _) => _ = DeleteProfileAsync(profile);
            item.Buttons = profile.ProfileType == ProfileType.Offline
                ? [btnDelete]
                : [btnCopy, btnDelete];

            _listPanel.Children.Add(item);

            // 头像后台解析（SkinHeadId 缓存 / 在线合成），完成后回填
            var captured = item;
            _ = Task.Run(() =>
            {
                var path = McSkinHelper.TryGetAvatarPath(profile);
                if (path is not null)
                    Dispatcher.UIThread.Post(() =>
                    {
                        if (captured.Tag == profile) captured.Logo = path;
                    });
            });
        }

        LogWrapper.Debug("Profile", $"档案列表刷新完成：{profiles.Count} 个档案");
    }

    /// <summary>选择档案（复刻 PageLoginProfileViewModel.Select）。</summary>
    private void SelectProfile(McProfile profile)
    {
        ProfileService.Select(profile);
        LogWrapper.Info("Profile", $"选定档案: {profile.UserName}, 以 {profile.ProfileType} 方式验证");
        // 重置登录 loader 状态，下次启动强制重新验证
        GameLaunch.mcLoginMsLoader.State = LoadState.Waiting;
        GameLaunch.mcLoginLegacyLoader.State = LoadState.Waiting;
        _host.RefreshPage();
        AppUi.Main?.NotifyProfileChanged();
    }

    /// <summary>新建档案：选择验证方式（复刻 ProfileUi.CreateProfileAsync 的 MVP 子集：微软/离线）。</summary>
    private async Task CreateProfileAsync()
    {
        ProfileService.IsCreatingProfile = true;
        var choice = await MyMsgBox.ShowAsync(
            Lang.Text("Launch.Account.Profile.Create.SelectAuthType.Title"),
            Lang.Text("Launch.Account.Profile.Create.SelectAuthType.Title"),
            [
                Lang.Text("Launch.Account.Type.Microsoft"),
                Lang.Text("Launch.Account.Type.Offline"),
                Lang.Text("Common.Action.Cancel"),
            ]);
        if (choice is not (1 or 2))
        {
            ProfileService.IsCreatingProfile = false;
            return;
        }

        // 由登录子页接管（LoginMsPage / LoginOfflinePage）
        _host.RefreshPage(choice == 1 ? GameLaunch.McLoginType.Ms : GameLaunch.McLoginType.Legacy);
    }

    private async Task DeleteProfileAsync(McProfile profile)
    {
        if (await MyMsgBox.ShowAsync(
                Lang.Text("Launch.Account.Profile.DeleteConfirm.Message"),
                Lang.Text("Launch.Account.Profile.DeleteConfirm.Title"),
                [Lang.Text("Common.Action.Continue"), Lang.Text("Common.Action.Cancel")],
                isWarn: true) != 1)
            return;

        ProfileService.Remove(profile);
        HintService.Show(Hints.Success(Lang.Text("Launch.Account.Profile.Deleted")));
        Reload();
        _host.RefreshPage();
    }
}
