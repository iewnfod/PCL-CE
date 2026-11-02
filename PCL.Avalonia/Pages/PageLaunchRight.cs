using System.Collections.Specialized;
using System.Net.Http;
using System.Text;
using Avalonia;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using PCL.Avalonia.Controls;
using PCL.Avalonia.Platform;
using PCL.Core.App.Localization;
using PCL.Core.Model.Tools.News;
using PCL.Core.ViewModel.Homepage;

namespace PCL.Avalonia.Pages;

/// <summary>
///     启动主页右侧页（复刻 WPF PageLaunchRight + PageHomepageNewsView）：
///     社区公告卡（MVP 固定文案）+ Minecraft 信息流瀑布流（真实 NewsViewModel）+ 启动日志卡。
/// </summary>
public class PageLaunchRight : Grid
{
    private static readonly HttpClient Http = new();
    private static readonly Dictionary<string, Bitmap> ImageCache = new();

    private readonly StackPanel _panMain = new() { Margin = new Thickness(25, 25, 25, 10) };
    private readonly WaterfallPanel _newsPanel = new()
    {
        Margin = new Thickness(0, 0, 0, 10),
        ColumnSpacing = 14,
        RowSpacing = 14,
        MaxItemWidth = 240,
    };

    private readonly MyCard _panLoad = new() { Margin = new Thickness(32), IsVisible = false };
    private readonly MyCard _panLog = new();
    private readonly TextBlock _labLog = new()
    {
        Margin = new Thickness(20, 0, 20, 18),
        TextWrapping = TextWrapping.Wrap,
        FontFamily = new FontFamily("Mono,Menlo,Consolas,monospace"),
        FontSize = 12,
    };

    private readonly StringBuilder _logBuilder = new();
    private readonly NewsViewModel _newsVm = new();
    private bool _logAutoScroll = true;

    public PageLaunchRight()
    {
        var scroll = new ScrollViewer
        {
            Content = _panMain,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
        };
        Children.Add(scroll);

        // ===== 社区公告卡（MVP：固定文案 + 关闭按钮） =====
        var panHint = new MyCard
        {
            Margin = new Thickness(0, 0, 0, 15),
            Title = Lang.Text("Launch.Right.CommunityHint.Title"),
        };
        var btnHintClose = new MyIconButton
        {
            SvgIcon = "lucide/x", Height = 20, Width = 20, LogoScale = 1.1,
            Margin = new Thickness(10), HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Top,
        };
        btnHintClose.Click += (_, _) => panHint.IsVisible = false;
        var hint1 = new TextBlock
        {
            Text = "你正在使用 PCL CE 的 Avalonia 跨平台预览版（MVP）。",
            TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 2), FontSize = 13.5,
        };
        var hint2 = new TextBlock
        {
            Text = "部分功能仍在开发中，遇到问题欢迎反馈至社区仓库。",
            TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 2), FontSize = 13.5,
        };
        panHint.Content = new Panel
        {
            Children =
            {
                new StackPanel { Margin = new Thickness(25, 0, 23, 15), Children = { hint1, hint2 } },
                btnHintClose,
            },
        };
        _panMain.Children.Add(panHint);

        // ===== 新闻瀑布流（真实数据） =====
        _panMain.Children.Add(_newsPanel);
        _panLoad.Content = new MyLoading
        {
            Margin = new Thickness(20, 0, 20, 17),
        };
        _panMain.Children.Add(_panLoad);

        _newsVm.NewsItems.CollectionChanged += OnNewsChanged;
        _newsVm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName != nameof(NewsViewModel.IsLoading)) return;
            Dispatcher.UIThread.Post(() =>
                _panLoad.IsVisible = _newsVm.IsLoading && _newsVm.NewsItems.Count == 0);
        };

        // ===== 启动日志卡 =====
        _panLog.Title = Lang.Text("Launch.Right.Log.Title");
        _panLog.Margin = new Thickness(0, 0, 0, 15);
        _panLog.Content = _labLog;
        _labLog.Text = "（暂无启动日志）";
        _panMain.Children.Add(_panLog);

        UiBridgeHub.LaunchLogAppended += text => Dispatcher.UIThread.Post(() => _AppendLog(text));
    }

    private void OnNewsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        Dispatcher.UIThread.Post(() =>
        {
            _newsPanel.Children.Clear();
            foreach (var item in _newsVm.NewsItems) _newsPanel.Children.Add(_BuildNewsCard(item));
            _panLoad.IsVisible = _newsVm.IsLoading && _newsVm.NewsItems.Count == 0;
        });
    }

    /// <summary>新闻卡片（1:1 复刻 PageHomepageNewsView 模板：图/标题底/作者/简介/查看详情）。</summary>
    private Control _BuildNewsCard(NewsItem item)
    {
        var grid = new Grid { Margin = new Thickness(12) };
        for (var i = 0; i < 5; i++) grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));

        // 图片（异步加载，失败显示占位底）
        var imageHost = new Border
        {
            CornerRadius = new CornerRadius(6),
            ClipToBounds = true,
            Height = 120,
        };
        imageHost.SetDynamic(Border.BackgroundProperty, "ColorBrush6");
        var image = new Image { Stretch = Stretch.UniformToFill };
        imageHost.Child = image;
        if (!string.IsNullOrEmpty(item.Image))
        {
            if (ImageCache.TryGetValue(item.Image, out var cached))
            {
                image.Source = cached;
            }
            else
            {
                _ = _LoadImageAsync(item.Image, bitmap =>
                {
                    image.Source = bitmap;
                    imageHost.Background = Brushes.Transparent;
                });
            }
        }

        Grid.SetRow(imageHost, 0);
        grid.Children.Add(imageHost);

        // 标题（背景 ColorBrushBackground 圆角 6、Padding 5,3、16 Bold ColorBrush1）
        var titleBorder = new Border
        {
            Margin = new Thickness(0, 5, 0, 0),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(5, 3),
            ClipToBounds = true,
            Child = new TextBlock
            {
                Text = item.Title,
                FontSize = 16,
                FontWeight = FontWeight.Bold,
                TextWrapping = TextWrapping.Wrap,
            },
        };
        titleBorder.SetDynamic(Border.BackgroundProperty, "ColorBrushBackground");
        titleBorder.SetDynamic(TextBlock.ForegroundProperty, "ColorBrush1");
        Grid.SetRow(titleBorder, 1);
        grid.Children.Add(titleBorder);

        // 作者 / 日期（Gray2）
        var metaGrid = new Grid { Margin = new Thickness(0, 5, 0, 0) };
        metaGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        metaGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        var labAuthor = new TextBlock { Text = item.Author };
        labAuthor.Bind(TextBlock.ForegroundProperty, new DynamicResourceExtension("ColorBrushGray2"));
        var labDate = new TextBlock
        {
            Text = item.PublishDate.ToString("g"),
            Margin = new Thickness(8, 0, 0, 0),
        };
        labDate.Bind(TextBlock.ForegroundProperty, new DynamicResourceExtension("ColorBrushGray2"));
        Grid.SetColumn(labDate, 1);
        metaGrid.Children.Add(labAuthor);
        metaGrid.Children.Add(labDate);
        Grid.SetRow(metaGrid, 2);
        grid.Children.Add(metaGrid);

        // 简介（Gray4）
        var labDesc = new TextBlock
        {
            Text = item.Description,
            Margin = new Thickness(0, 5, 0, 0),
            TextWrapping = TextWrapping.Wrap,
        };
        labDesc.Bind(TextBlock.ForegroundProperty, new DynamicResourceExtension("ColorBrushGray4"));
        Grid.SetRow(labDesc, 3);
        grid.Children.Add(labDesc);

        // 查看详情（右下文字按钮，Gray2 → hover ColorBrush2）
        var btnRead = new Button
        {
            Content = Lang.Text("Launch.Homepage.News.ViewDetail"),
            Margin = new Thickness(0, 5, 5, 0),
            HorizontalAlignment = HorizontalAlignment.Right,
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(4, 2),
            Cursor = new Cursor(StandardCursorType.Hand),
        };
        btnRead.Bind(Button.ForegroundProperty, new DynamicResourceExtension("ColorBrushGray2"));
        btnRead.Click += (_, _) =>
        {
            // 链接来自 minecraft.net 官方信息流 API；仅允许 http(s)
            if (Uri.TryCreate(item.Url, UriKind.Absolute, out var uri) &&
                uri.Scheme is "http" or "https")
                PCL.Core.App.Basics.OpenPath(item.Url!);
        };
        Grid.SetRow(btnRead, 4);
        grid.Children.Add(btnRead);

        return new MyCard { Content = grid };
    }

    private static async Task _LoadImageAsync(string url, Action<Bitmap> onLoaded)
    {
        try
        {
            if (ImageCache.TryGetValue(url, out var cached))
            {
                Dispatcher.UIThread.Post(() => onLoaded(cached));
                return;
            }

            var bytes = await Http.GetByteArrayAsync(url);
            var bitmap = await Task.Run(() =>
            {
                using var stream = new MemoryStream(bytes);
                return new Bitmap(stream);
            });
            ImageCache[url] = bitmap;
            Dispatcher.UIThread.Post(() => onLoaded(bitmap));
        }
        catch
        {
            // 网络失败：保持占位底色
        }
    }

    private void _AppendLog(string text)
    {
        if (_logBuilder.Length == 0) _labLog.Text = "";
        _logBuilder.AppendLine(text);
        if (_logBuilder.Length > 200_000) _labLog.Text = _logBuilder.ToString()[^100_000..];
        else _labLog.Text = _logBuilder.ToString();
        // 滚动到底（日志卡在滚动容器内，滚动外层 ScrollView）
        if (_logAutoScroll && _panMain.Parent is ScrollViewer scroll)
            Dispatcher.UIThread.Post(() => scroll.ScrollToEnd(), DispatcherPriority.Background);
    }
}
