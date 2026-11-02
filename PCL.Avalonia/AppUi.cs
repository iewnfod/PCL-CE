namespace PCL.Avalonia;

/// <summary>
///     UI 导航入口（对应 WPF Launcher.frmMain.PageChange 的 MVP 子集）：
///     主 tab 切换 + 启动 tab 内「主页 / 实例选择」子页切换。
/// </summary>
public static class AppUi
{
    public static MainWindow? Main { get; internal set; }

    /// <summary>切到启动 tab 的主页子页（登录 + 启动按钮 + 公告/新闻/日志）。</summary>
    public static void NavigateToLaunchHome() => Main?.ShowLaunchHome();

    /// <summary>切到启动 tab 的实例选择子页（文件夹列表 + 实例卡片）。</summary>
    public static void NavigateToInstanceSelect() => Main?.ShowInstanceSelect();

    /// <summary>切到指定主 tab。</summary>
    public static void NavigateToTab(MainTab tab) => Main?.ShowTab(tab);
}

public enum MainTab
{
    Launch,
    Download,
    Setup,
    Tools
}
