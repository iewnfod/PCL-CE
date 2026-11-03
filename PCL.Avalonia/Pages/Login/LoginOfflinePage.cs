using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using PCL.Avalonia.Controls;
using PCL.Avalonia.Platform;
using PCL.Core.App.Localization;
using PCL.Core.Logging;
using PCL.Core.Minecraft.Profile;
using PCL.Core.Minecraft.Profile.Models;
using PCL.Core.UI.Hints;

namespace PCL.Avalonia.Pages.Login;

/// <summary>
///     离线登录页（复刻 WPF PageLoginOffline）：玩家名输入 + UUID 生成方式三选一
///     （标准 / 旧版 / 自定义，自定义显示 32 位输入）+ 创建 / 返回。
/// </summary>
public class LoginOfflinePage : Grid
{
    private enum UuidMode
    {
        Standard,
        Legacy,
        Custom,
    }

    private readonly LoginPageHost _host;
    private readonly MyTextBox _textName = new() { Watermark = Lang.Text("Launch.Account.Offline.PlayerId") };
    private readonly MyTextBox _textUuid = new() { Watermark = "UUID" };
    private readonly MyRadioButton _radioStandard;
    private readonly MyRadioButton _radioLegacy;
    private readonly MyRadioButton _radioCustom;
    private readonly TextBlock _labUuidTitle = new() { Text = "UUID", VerticalAlignment = VerticalAlignment.Center };
    private UuidMode _mode = UuidMode.Standard;

    public LoginOfflinePage(LoginPageHost host)
    {
        _host = host;
        VerticalAlignment = VerticalAlignment.Center;
        ColumnDefinitions.Add(new ColumnDefinition(50, GridUnitType.Pixel));
        ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));

        for (var i = 0; i < 10; i++)
            RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        // 行距通过 Margin 实现（对齐 WPF 固定行高节奏）
        _textName.Margin = new Thickness(0, 0, 0, 10);

        // Row 0：玩家名
        var labName = new TextBlock
        {
            Text = Lang.Text("Launch.Account.Offline.PlayerId"),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            TextWrapping = TextWrapping.Wrap,
        };
        Grid.SetRow(labName, 0);
        Children.Add(labName);
        Grid.SetRow(_textName, 0);
        Grid.SetColumn(_textName, 1);
        Children.Add(_textName);
        _textName.EnterPressed += (_, _) => _ = LoginAsync();

        // Row 2：UUID 生成方式标题（跨全宽居中，对齐 WPF 负 margin 视觉）
        var labUuidMode = new TextBlock
        {
            Text = Lang.Text("Launch.Account.Offline.UuidStandard"),
            FontWeight = FontWeight.Bold,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 0, 0, 8),
        };
        Grid.SetRow(labUuidMode, 2);
        Grid.SetColumnSpan(labUuidMode, 2);
        Children.Add(labUuidMode);

        // Row 3：三个单选（跨全宽居中，避免超出左栏被裁切）
        MyRadioButton Radio(string text, string toolTip)
        {
            var radio = new MyRadioButton
            {
                Text = text,
                ColorType = MyRadioButtonColorType.RadioBox,
                FontSize = 12.5,
                Height = 22,
                GroupName = "OfflineUuidMode",
            };
            ToolTip.SetTip(radio, toolTip);
            return radio;
        }

        _radioStandard = Radio(Lang.Text("Launch.Account.Offline.Uuid.Standard"),
            Lang.Text("Launch.Account.Offline.Uuid.Standard.ToolTip"));
        _radioLegacy = Radio(Lang.Text("Launch.Account.Offline.Uuid.Legacy"),
            Lang.Text("Launch.Account.Offline.Uuid.Legacy.ToolTip"));
        _radioCustom = Radio(Lang.Text("Common.Option.Customize"),
            Lang.Text("Launch.Account.Offline.Uuid.Custom.ToolTip"));
        _radioStandard.Checked = true;

        var radioPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Spacing = 6,
            Margin = new Thickness(0, 0, 0, 14),
            Children = { _radioStandard, _radioLegacy, _radioCustom },
        };
        Grid.SetRow(radioPanel, 3);
        Grid.SetColumnSpan(radioPanel, 2);
        Children.Add(radioPanel);

        // Row 5：自定义 UUID 输入（仅自定义模式显示）
        _labUuidTitle.HorizontalAlignment = HorizontalAlignment.Center;
        _textUuid.Margin = new Thickness(0, 0, 0, 14);
        _labUuidTitle.IsVisible = false;
        _textUuid.IsVisible = false;
        Grid.SetRow(_labUuidTitle, 5);
        Children.Add(_labUuidTitle);
        Grid.SetRow(_textUuid, 5);
        Grid.SetColumn(_textUuid, 1);
        Children.Add(_textUuid);

        _radioStandard.Check += (_, _) => _SetMode(UuidMode.Standard);
        _radioLegacy.Check += (_, _) => _SetMode(UuidMode.Legacy);
        _radioCustom.Check += (_, _) => _SetMode(UuidMode.Custom);

        // Row 8：返回 + 创建
        var btnBack = new MyButton
        {
            Text = Lang.Text("Launch.Account.Offline.Back"),
            Height = 28,
            Padding = new Thickness(14, 0),
            ColorType = MyButtonColorType.Normal,
        };
        btnBack.Click += (_, _) => Back();
        var btnLogin = new MyButton
        {
            Text = Lang.Text("Launch.Account.Offline.Create"),
            Height = 28,
            Padding = new Thickness(14, 0),
            ColorType = MyButtonColorType.Highlight,
        };
        btnLogin.Click += (_, _) => _ = LoginAsync();
        var buttonPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 8, 0, 0),
            Spacing = 15,
            Children = { btnBack, btnLogin },
        };
        Grid.SetRow(buttonPanel, 8);
        Grid.SetColumnSpan(buttonPanel, 2);
        Children.Add(buttonPanel);
    }

    private void _SetMode(UuidMode mode)
    {
        _mode = mode;
        var isCustom = mode == UuidMode.Custom;
        _textUuid.IsVisible = isCustom;
        _labUuidTitle.IsVisible = isCustom;
    }

    private void Back()
    {
        ProfileService.IsCreatingProfile = false;
        _host.RefreshPage();
    }

    /// <summary>创建离线档案（复刻 PageLoginOfflineViewModel.LoginAsync，含名称/UUID 校验）。</summary>
    private async Task LoginAsync()
    {
        try
        {
            var username = _textName.Text.Trim();
            if (!System.Text.RegularExpressions.Regex.IsMatch(username, "^[A-z0-9_]{3,16}$"))
            {
                var result = await MyMsgBox.ShowAsync(
                    Lang.Text("Launch.Account.Offline.InvalidPlayerId.Message"),
                    Lang.Text("Launch.Account.Offline.InvalidPlayerId.Title"),
                    [Lang.Text("Common.Action.Continue"), Lang.Text("Common.Action.Cancel")],
                    isWarn: true);
                if (result == 2) return;
            }

            string userUuid;
            if (_mode == UuidMode.Custom)
            {
                var uuidInput = _textUuid.Text.Replace("-", "");
                if (!System.Text.RegularExpressions.Regex.IsMatch(uuidInput, "^[a-fA-F0-9]{32}$"))
                {
                    HintService.Show(Hints.Error(Lang.Text("Launch.Account.Offline.InvalidUuid", uuidInput)));
                    return;
                }

                userUuid = uuidInput;
            }
            else if (_mode == UuidMode.Legacy)
            {
                userUuid = AvaloniaProfileUi.GetLegacyOfflineUuid(username);
            }
            else
            {
                userUuid = AvaloniaProfileUi.GetOfflineUuid(username);
            }

            var newProfile = new McProfile
            {
                ProfileType = ProfileType.Offline,
                Uuid = userUuid,
                UserName = username,
                Description = "",
                AccessToken = userUuid,
                ClientToken = userUuid,
                ProfileId = Guid.NewGuid().ToString("N"),
            };
            ProfileService.Add(newProfile);
            ProfileService.IsCreatingProfile = false;
            HintService.Show(Hints.Success(Lang.Text("Launch.Account.Profile.Created")));
            LogWrapper.Info("Profile", $"离线档案创建成功：{username}（{userUuid}）");

            _host.RefreshPage();
            AppUi.Main?.NotifyProfileChanged();
        }
        catch (Exception ex)
        {
            LogWrapper.Error(ex, "Profile", "创建离线档案失败");
            HintService.Show(Hints.Error("创建离线档案失败：" + ex.Message));
        }
    }
}
