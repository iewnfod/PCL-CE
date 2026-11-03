using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using PCL.Avalonia.Controls;
using PCL.Core.App.Localization;

namespace PCL.Avalonia.Pages;

/// <summary>
///     下载页左侧导航（复刻 WPF PageDownloadLeft 的 MVP 子集）：
///     Minecraft（版本安装，真实子页）+ 社区资源/安装包占位组 + 刷新按钮。
/// </summary>
public class PageDownloadLeft : StackPanel
{
    private readonly PageDownloadInstall _installPage;
    private readonly List<MyListItem> _navItems = [];
    private MyIconButton? _btnRefresh;

    public PageDownloadLeft(PageDownloadInstall installPage)
    {
        _installPage = installPage;
        Margin = new Thickness(0, 18, 5, 4);

        // 刷新：重新拉取版本清单
        _btnRefresh = new MyIconButton
        {
            SvgIcon = "lucide/refresh-cw",
            LogoScale = 1.05,
            Margin = new Thickness(13, 0, 0, 10),
            HorizontalAlignment = HorizontalAlignment.Left,
        };
        ToolTip.SetTip(_btnRefresh, Lang.Text("Common.Action.Refresh"));
        _btnRefresh.Click += (_, _) =>
        {
            _installPage.ExitSelectPage();
            GameDownload.dlClientListLoader.Start(0, true);
            GameDownload.DlFabricListLoader.Start(0, true);
            GameDownload.DlNeoForgeListLoader.Start(0, true);
        };

        AddGroup(null);
        AddItem("Minecraft", "lucide/box", true, () => { });
        AddGroup("社区资源");
        AddItem("模组", "lucide/grid-2x2", false, Placeholder);
        AddItem("整合包", "lucide/package", false, Placeholder);
        AddItem("数据包", "lucide/server", false, Placeholder);
        AddItem("资源包", "lucide/file-archive", false, Placeholder);
        AddItem("光影包", "lucide/sparkles", false, Placeholder);
        AddItem("世界", "lucide/globe", false, Placeholder);
        AddItem("收藏夹", "lucide/heart", false, Placeholder);
        AddGroup("安装包");
        AddItem("Minecraft", "lucide/box", false, Placeholder);
        AddItem("OptiFine", "lucide/gauge", false, Placeholder);
        AddItem("Forge", "lucide/anvil", false, Placeholder);
        AddItem("NeoForge", "lucide/anvil", false, Placeholder);

        Children.Add(_btnRefresh);
    }

    private static void Placeholder()
    {
        PCL.Core.UI.Hints.HintService.Show(new()
        {
            Message = "该子页将在后续版本提供（MVP 占位）",
            Kind = PCL.Core.UI.Hints.HintKind.Info,
        });
    }

    private void AddGroup(string? group)
    {
        if (group is null) return;
        Children.Add(new TextBlock
        {
            Text = group,
            Opacity = 0.6,
            FontSize = 12,
            Margin = new Thickness(13, 9, 0, 4),
        });
    }

    private void AddItem(string title, string icon, bool @checked, Action onClick)
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
            // 导航互斥：取消其他项的选中态（竖条/高亮随之消失）
            foreach (var other in _navItems)
                if (!ReferenceEquals(other, sender) && other.Checked)
                    other.SetChecked(false, false, false);
            onClick();
        };
        if (@checked) item.Checked = true;
        _navItems.Add(item);
        Children.Add(item);
    }
}
