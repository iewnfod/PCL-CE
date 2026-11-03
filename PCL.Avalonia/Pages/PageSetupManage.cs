using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using PCL.Avalonia.Controls;
using PCL.Core.App;
using PCL.Core.App.Localization;

namespace PCL.Avalonia.Pages;

/// <summary>
///     设置-管理页（复刻 WPF PageSetupGameManage 的下载源子集）：
///     文件下载源 / 版本列表源（尽量镜像 / 官方优先失败镜像 / 尽量官方）。
/// </summary>
public class PageSetupManage : Grid
{
    private readonly MyComboBox _comboFileSource = new();
    private readonly MyComboBox _comboVersionSource = new();

    private static readonly string[] SourceItems =
    [
        Lang.Text("Setup.GameManage.Source.UseMirror"),
        Lang.Text("Setup.GameManage.Source.PreferOfficialThenMirror"),
        Lang.Text("Setup.GameManage.Source.UseOfficial"),
    ];

    private static readonly string[] VersionSourceItems =
    [
        Lang.Text("Setup.GameManage.Source.Version.Mirror"),
        Lang.Text("Setup.GameManage.Source.PreferOfficialThenMirror"),
        Lang.Text("Setup.GameManage.Source.UseOfficial"),
    ];

    public PageSetupManage()
    {
        var scroll = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
        };
        var panMain = new StackPanel { Margin = new Thickness(25, 25, 25, 10) };
        scroll.Content = panMain;
        Children.Add(scroll);

        var card = new MyCard
        {
            Margin = new Thickness(0, 0, 0, 15),
            Title = Lang.Text("Setup.GameManage.Source.Title"),
        };
        var pan = new StackPanel { Margin = new Thickness(25, 4, 25, 15) };

        pan.Children.Add(_ComboRow(Lang.Text("Setup.GameManage.Source.File"), _comboFileSource, SourceItems,
            () => Config.Download.FileSource, v => Config.Download.FileSource = v));
        pan.Children.Add(_ComboRow(Lang.Text("Setup.GameManage.Source.Version"), _comboVersionSource, VersionSourceItems,
            () => Config.Download.VersionListSource, v => Config.Download.VersionListSource = v));

        card.Content = pan;
        panMain.Children.Add(card);
    }

    private static Control _ComboRow(string label, MyComboBox combo, string[] items,
        Func<int> get, Action<int> set)
    {
        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*"),
            Margin = new Thickness(0, 0, 0, 9),
        };
        var text = new TextBlock
        {
            Text = label,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new Thickness(0, 0, 25, 0),
            FontSize = 13,
        };
        text.SetDynamic(TextBlock.ForegroundProperty, "ColorBrushMemory");
        Grid.SetColumn(text, 0);
        combo.Items = items;
        combo.SetSelected(Math.Clamp(get(), 0, items.Length - 1), false);
        combo.SelectionChanged += (_, index, user) =>
        {
            if (user && index >= 0 && index < items.Length) set(index);
        };
        Grid.SetColumn(combo, 1);
        combo.HorizontalAlignment = HorizontalAlignment.Stretch;
        grid.Children.Add(text);
        grid.Children.Add(combo);
        return grid;
    }
}
