using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using PCL.Avalonia.Controls;
using PCL.Core.App.Localization;

namespace PCL.Avalonia.Pages;

/// <summary>设置页子页标识（对齐 WPF PageSetupLeft 导航项）。</summary>
public enum SetupPage
{
    Launch,      // 启动
    Java,        // Java
    Manage,      // 管理（下载源等）
    GameLink,    // 联机
    Appearance,  // 个性化
    Language,    // 语言
    Misc,        // 杂项
    About,       // 软件信息
    Update,      // 软件更新
    Feedback,    // 反馈
    Log,         // 查看日志
}

/// <summary>
///     设置页左侧导航（复刻 WPF PageSetupLeft 分组结构）：
///     游戏（启动/Java/管理）、工具（联机）、启动器（个性化/语言/杂项）、关于（信息/更新/反馈/日志）。
/// </summary>
public class PageSetupLeft : StackPanel
{
    /// <summary>导航项点击（含初始选中项）。</summary>
    public event Action<SetupPage>? PageSelected;

    private readonly Dictionary<SetupPage, MyListItem> _items = [];

    public PageSetupLeft()
    {
        Margin = new Thickness(0, 18, 5, 4);

        AddGroup(Lang.Text("Setup.Left.Category.Game"));
        AddItem(SetupPage.Launch, Lang.Text("Setup.Left.Item.Launch"), "lucide/rocket");
        AddItem(SetupPage.Java, Lang.Text("Setup.Left.Item.Java"), "lucide/coffee");
        AddItem(SetupPage.Manage, Lang.Text("Setup.Left.Item.GameManage"), "lucide/file-archive");
        AddGroup(Lang.Text("Setup.Left.Category.Tools"));
        AddItem(SetupPage.GameLink, Lang.Text("Setup.Left.Item.GameLink"), "lucide/user");
        AddGroup(Lang.Text("Setup.Left.Category.Launcher"));
        AddItem(SetupPage.Appearance, Lang.Text("Setup.Left.Item.Ui"), "lucide/palette");
        AddItem(SetupPage.Language, Lang.Text("Setup.Language.Title"), "lucide/globe");
        AddItem(SetupPage.Misc, Lang.Text("Setup.Left.Item.Misc"), "lucide/monitor-cog");
        AddGroup(Lang.Text("Setup.Left.Category.About"));
        AddItem(SetupPage.About, Lang.Text("Setup.Left.Item.About"), "lucide/info");
        AddItem(SetupPage.Update, Lang.Text("Setup.Left.Item.Update"), "lucide/refresh-cw");
        AddItem(SetupPage.Feedback, Lang.Text("Setup.Left.Item.Feedback"), "lucide/message-circle");
        AddItem(SetupPage.Log, Lang.Text("Setup.Left.Item.Log"), "lucide/scroll-text");
    }

    /// <summary>程序选中某子页（同步左栏选中态并触发事件）。</summary>
    public void Select(SetupPage page)
    {
        // 导航项显式互斥（Clickable 类型默认可多选）
        foreach (var (key, item) in _items)
            item.SetChecked(key == page, false, false);
    }

    private void AddGroup(string group)
    {
        Children.Add(new TextBlock
        {
            Text = group,
            Opacity = 0.6,
            FontSize = 12,
            Margin = new Thickness(13, 9, 0, 4),
        });
    }

    private void AddItem(SetupPage page, string title, string icon)
    {
        var item = new MyListItem
        {
            IsScaleAnimationEnabled = false,
            Type = MyListItemCheckType.Clickable,
            Title = title,
            Height = 34,
            SvgIcon = icon,
            LogoScale = 0.9,
        };
        item.Click += (sender, _) =>
        {
            if (sender is MyListItem self && !self.Checked) self.Checked = true;
            Select(page);
            PageSelected?.Invoke(page);
        };
        _items[page] = item;
        Children.Add(item);
    }
}
