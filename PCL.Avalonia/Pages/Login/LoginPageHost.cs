using Avalonia.Controls;
using PCL.Core.Logging;
using PCL.Core.Minecraft.Profile;

namespace PCL.Avalonia.Pages.Login;

/// <summary>
///     左栏登录区宿主（复刻 WPF PageLaunchLeft 的 PanLogin + RefreshPage 体系）：
///     管理四个登录子页（档案列表 / 档案详情 / 微软登录 / 离线登录）并按档案状态切换。
/// </summary>
public class LoginPageHost : Grid
{
    private enum PageType
    {
        None,
        ProfileList,
        ProfileDetail,
        Ms,
        Offline,
    }

    private PageType _current = PageType.None;

    private LoginProfileListPage? _listPage;
    private LoginProfileDetailPage? _detailPage;
    private LoginMsPage? _msPage;
    private LoginOfflinePage? _offlinePage;

    /// <summary>登录区子页直达钩子（PCL_LOGIN_PAGE=list/detail/ms/offline，供自动化视觉验证）。</summary>
    public static string? ForcedPage { get; } = Environment.GetEnvironmentVariable("PCL_LOGIN_PAGE");

    public LoginPageHost()
    {
        // 子页面在首次切换时按需构造
    }

    /// <summary>确认当前显示的子页面正确并切换（复刻 PageLaunchLeft.RefreshPage）。</summary>
    /// <param name="targetLoginType">正在创建档案时的目标验证方式，否则为 null。</param>
    public void RefreshPage(GameLaunch.McLoginType? targetLoginType = null)
    {
        PageType type;
        if (targetLoginType is { } loginType)
        {
            type = loginType switch
            {
                GameLaunch.McLoginType.Ms => PageType.Ms,
                _ => PageType.Offline,
            };
        }
        else if (ProfileService.Current is not null)
        {
            type = PageType.ProfileDetail;
        }
        else
        {
            type = PageType.ProfileList;
        }

        // 自动化钩子：强制直达指定子页（无视档案状态）
        switch (ForcedPage)
        {
            case "list":
                type = PageType.ProfileList;
                break;
            case "detail":
                type = PageType.ProfileDetail;
                break;
            case "ms":
                type = PageType.Ms;
                break;
            case "offline":
                type = PageType.Offline;
                break;
        }

        if (_current == type) return;
        _current = type;

        Control page = type switch
        {
            PageType.ProfileList => _listPage ??= new LoginProfileListPage(this),
            PageType.ProfileDetail => _detailPage ??= new LoginProfileDetailPage(this),
            PageType.Ms => _msPage ??= new LoginMsPage(this),
            _ => _offlinePage ??= new LoginOfflinePage(this),
        };

        Children.Clear();
        Children.Add(page);
        LogWrapper.Debug("Profile", $"登录区切换子页：{type}");
    }
}
