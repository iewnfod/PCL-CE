using Avalonia.Threading;
using PCL.Core.App.Localization;
using PCL.Core.Minecraft.IdentityModel;
using PCL.Core.Minecraft.IdentityModel.OAuth;
using PCL.Core.Minecraft.Profile;
using PCL.Core.Minecraft.Profile.Authentication;
using PCL.Core.Minecraft.Profile.Models;
using PCL.Core.UI.Hints;
using PCL.Avalonia.Controls;

namespace PCL.Avalonia.Platform;

/// <summary>
///     档案登录 UI 桥（Avalonia 版，逻辑复刻 WPF Modules/Minecraft/ProfileUi.cs 的 MVP 子集）：
///     按当前档案生成登录数据、离线档案校验、设备码登录弹窗、微软账号类错误指引。
/// </summary>
public sealed class AvaloniaProfileUi : IProfileUi
{
    public object? GetLoginData()
    {
        var profile = ProfileService.Current;
        if (profile is null) return new GameLaunch.McLoginMs();
        return profile.ProfileType switch
        {
            ProfileType.Microsoft => new GameLaunch.McLoginMs(),
            _ => new GameLaunch.McLoginLegacy { UserName = profile.UserName, Uuid = profile.Uuid },
        };
    }

    public string IsProfileValid()
    {
        var profile = ProfileService.Current;
        if (profile is null) return Lang.Text("Minecraft.Launch.Precheck.NoProfile");
        if (profile.ProfileType != ProfileType.Offline) return string.Empty;
        if (string.IsNullOrWhiteSpace(profile.UserName)) return Lang.Text("Launch.Account.Profile.Validation.EmptyUsername");
        if (profile.UserName.Contains('"')) return Lang.Text("Launch.Account.Profile.Validation.QuoteInUsername");
        return string.Empty;
    }

    public Task<AuthorizeResult?> ShowDeviceCodeLoginAsync(
        DeviceCodeAuthenticationContext context,
        CancellationToken token)
    {
        return DeviceCodeLoginDialog.ShowAsync(context, token);
    }

    public bool HandleMicrosoftXstsError(IdentityModelAuthenticationException exception)
    {
        if (exception.Error is not { } error || !error.StartsWith("xsts_", StringComparison.OrdinalIgnoreCase))
            return false;

        var normalized = error.ToLowerInvariant();
        switch (normalized)
        {
            case "xsts_2148916227":
                // 账号被封禁
                MyMsgBox.Show(
                    Lang.Text("Minecraft.Launch.Login.Microsoft.Banned"),
                    Lang.Text("Minecraft.Launch.Login.Failed"), isWarn: true);
                return true;
            case "xsts_2148916233":
                // 未注册 Xbox 账户
                if (MyMsgBox.Show(
                        Lang.Text("Minecraft.Launch.Login.Microsoft.XboxNotRegistered"),
                        Lang.Text("Minecraft.Launch.Login.Hint"),
                        [Lang.Text("Minecraft.Launch.Login.Register"), Lang.Text("Common.Action.Cancel")]) == 1)
                    PCL.LauncherUi.Interaction?.OpenWebsite("https://signup.live.com/signup");
                return true;
            case "xsts_2148916235":
                // 所在地区不受支持
                MyMsgBox.Show(
                    Lang.Text("Minecraft.Launch.Login.Microsoft.RegionBlocked"),
                    Lang.Text("Minecraft.Launch.Login.Failed"), isWarn: true);
                return true;
            case "xsts_2148916238":
                // 未成年人账户
                var choice = MyMsgBox.Show(
                    Lang.Text("Minecraft.Launch.Login.Microsoft.Underage.Message"),
                    Lang.Text("Minecraft.Launch.Login.Hint"),
                    [
                        Lang.Text("Minecraft.Launch.Login.Microsoft.Underage.AgeOver13"),
                        Lang.Text("Minecraft.Launch.Login.Microsoft.Underage.AgeUnder13"),
                        Lang.Text("Common.Option.IDontKnow"),
                    ]);
                if (choice == 1)
                    PCL.LauncherUi.Interaction?.OpenWebsite("https://account.live.com/editprof.aspx");
                else if (choice == 2)
                    PCL.LauncherUi.Interaction?.OpenWebsite(
                        "https://support.microsoft.com/zh-cn/account-billing/如何更改-microsoft-帐户上的出生日期-837badbc-999e-54d2-2617-d19206b9540a");
                if (choice is 1 or 2)
                    MyMsgBox.Show(Lang.Text("Minecraft.Launch.Login.Microsoft.ChangeBirthDate.Message"),
                        Lang.Text("Minecraft.Launch.Login.Hint"));
                return true;
            default:
                return false;
        }
    }

    public bool HandleMicrosoftNotOwnedError(IdentityModelAuthenticationException exception)
    {
        if (!string.Equals(exception.Error, "minecraft_not_owned", StringComparison.OrdinalIgnoreCase))
            return false;

        Dispatcher.UIThread.Post(() => _ = _HandleNotOwnedAsync());
        return true;
    }

    private static async Task _HandleNotOwnedAsync()
    {
        if (await MyMsgBox.ShowAsync(
                Lang.Text("Minecraft.Launch.Login.Microsoft.NotPurchased"),
                Lang.Text("Minecraft.Launch.Login.Failed"),
                [Lang.Text("Minecraft.Launch.Login.Microsoft.PurchaseMinecraft"), Lang.Text("Common.Action.Cancel")],
                isWarn: true) == 1)
            PCL.LauncherUi.Interaction?.OpenWebsite(
                "https://www.xbox.com/zh-cn/games/store/minecraft-java-bedrock-edition-for-pc/9nxp44l49shj");
    }

    public bool HandleMicrosoftCreateProfileError(IdentityModelAuthenticationException exception)
    {
        if (!string.Equals(exception.Error, "profile_not_created", StringComparison.OrdinalIgnoreCase))
            return false;

        Dispatcher.UIThread.Post(() => _ = _HandleCreateProfileAsync());
        return true;
    }

    private static async Task _HandleCreateProfileAsync()
    {
        if (await MyMsgBox.ShowAsync(
                Lang.Text("Minecraft.Launch.Login.Microsoft.CreateProfile.Message"),
                Lang.Text("Minecraft.Launch.Login.Failed"),
                [Lang.Text("Minecraft.Launch.Login.Microsoft.CreateProfile.Button"), Lang.Text("Common.Action.Cancel")],
                isWarn: true) == 1)
            PCL.LauncherUi.Interaction?.OpenWebsite("https://www.minecraft.net/zh-hans/msaprofile/mygames/editprofile");
    }

    /// <summary>档案类型描述（复刻 ProfileUi.GetProfileInfo）。</summary>
    public static string GetProfileInfo(McProfile profile)
    {
        var info = profile.ProfileType switch
        {
            ProfileType.Microsoft => Lang.Text("Launch.Account.Type.Microsoft"),
            _ => Lang.Text("Launch.Account.Type.Offline"),
        };
        return string.IsNullOrWhiteSpace(profile.Description) ? info : $"{info}，{profile.Description}";
    }

    /// <summary>标准离线 UUID：UUID v3(MD5, "OfflinePlayer:" + name)，与离线服务器一致。</summary>
    public static string GetOfflineUuid(string name)
    {
        var md5 = System.Security.Cryptography.MD5.HashData(
            System.Text.Encoding.UTF8.GetBytes("OfflinePlayer:" + name));
        md5[6] = (byte)((md5[6] & 0x0F) | 0x30);
        md5[8] = (byte)((md5[8] & 0x3F) | 0x80);
        return new Guid(md5).ToString("N");
    }

    /// <summary>旧版离线 UUID（复刻 ProfileUi.GetOfflineUuid(isLegacy)）。</summary>
    public static string GetLegacyOfflineUuid(string userName)
    {
        // WPF: Common.StrFill(s, "0", n) = 左补零至 n 位
        string StrFill(string s, int len) => s.PadLeft(len, '0');
        var fullUuid = StrFill(userName.Length.ToString("X"), 16) +
                       StrFill(LauncherCommon.GetHash(userName).ToString("X"), 16);
        return fullUuid.Substring(0, 12) + "3" + fullUuid.Substring(13, 3) + "9" + fullUuid.Substring(17, 15);
    }
}
