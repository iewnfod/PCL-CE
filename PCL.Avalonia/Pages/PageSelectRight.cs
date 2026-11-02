using Avalonia;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using PCL.Avalonia.Controls;
using PCL.Core.App.Localization;

namespace PCL.Avalonia.Pages;

/// <summary>
///     实例选择右侧页（复刻 WPF PageSelectRight）：搜索框 + 按类型分组的实例卡片列表。<br/>
///     卡片（MyCard，标题带计数）内每实例一个 MyListItem（高 42、Logo、版本标签、右侧按钮组）；
///     空状态 / 搜索无结果 / 加载中 三种替代视图。
/// </summary>
public class PageSelectRight : Grid
{
    private readonly MySearchBox _searchBox = new()
    {
        Margin = new Thickness(25, 15, 25, 12),
        VerticalAlignment = VerticalAlignment.Top,
    };

    private readonly ScrollViewer _scroll = new()
    {
        VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
        HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
    };

    private readonly StackPanel _panMain = new() { Margin = new Thickness(25, 2, 25, 10) };
    private readonly MyCard _panLoad = new() { Margin = new Thickness(40), IsVisible = false };
    private readonly MyCard _panEmpty = new() { Margin = new Thickness(40), IsVisible = false };
    private readonly MyCard _panEmptySearch = new() { Margin = new Thickness(40), IsVisible = false };

    private string _searchText = "";
    private bool _loaderHooked;

    public PageSelectRight()
    {
        RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        RowDefinitions.Add(new RowDefinition(GridLength.Star));

        _scroll.Content = _panMain;
        SetRow(_scroll, 1);
        SetRow(_searchBox, 0);
        Children.Add(_searchBox);
        Children.Add(_scroll);

        // 空状态卡（标题 + 分隔线 + 内容 + 下载按钮）
        var emptyTitle = new TextBlock
        {
            FontSize = 19, HorizontalAlignment = HorizontalAlignment.Center,
        };
        emptyTitle.SetDynamic(TextBlock.ForegroundProperty, "ColorBrush3");
        var emptyRect = new Rectangle { Height = 2 };
        emptyRect.SetDynamic(Rectangle.FillProperty, "ColorBrush3");
        var emptyContent = new TextBlock        {
            Margin = new Thickness(10, 15, 10, 5), TextWrapping = TextWrapping.Wrap,
        };
        var btnDownload = new MyButton
        {
            Text = Lang.Text("Select.Instance.Empty.Download"),
            MinWidth = 140, Height = 35, Margin = new Thickness(0, 10, 0, 0),
            Padding = new Thickness(13, 0),
            HorizontalAlignment = HorizontalAlignment.Center,
            ColorType = MyButtonColorType.Highlight,
        };
        btnDownload.Click += (_, _) => AppUi.NavigateToTab(MainTab.Download);
        _panEmpty.Content = new StackPanel
        {
            Margin = new Thickness(20, 0),
            Children =
            {
                emptyTitle,
                emptyRect,
                emptyContent,
                btnDownload,
            },
        };

        // 搜索无结果卡
        var emptySearchTitle = new TextBlock
        {
            FontSize = 19, HorizontalAlignment = HorizontalAlignment.Center,
        };
        emptySearchTitle.SetDynamic(TextBlock.ForegroundProperty, "ColorBrush3");
        var emptySearchRect = new Rectangle { Height = 2 };
        emptySearchRect.SetDynamic(Rectangle.FillProperty, "ColorBrush3");
        var emptySearchContent = new TextBlock
        {
            Margin = new Thickness(10, 15, 10, 5), TextWrapping = TextWrapping.Wrap,
        };
        _panEmptySearch.Content = new StackPanel
        {
            Margin = new Thickness(20, 0),
            Children =
            {
                emptySearchTitle,
                emptySearchRect,
                emptySearchContent,
            },
        };

        // 加载卡
        _panLoad.Content = new MyLoading
        {
            Margin = new Thickness(20, 0, 20, 17),
            IsHitTestVisible = false,
        };

        Grid.SetRow(_panEmpty, 1);
        Grid.SetRow(_panEmptySearch, 1);
        Grid.SetRow(_panLoad, 1);
        Children.Add(_panEmpty);
        Children.Add(_panEmptySearch);
        Children.Add(_panLoad);

        _searchBox.SearchChanged += (_, e) =>
        {
            _searchText = (_searchBox.Text ?? "").Trim().ToLower();
            RefreshInstanceList();
        };
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (_loaderHooked) return;
        _loaderHooked = true;
        InstanceList.mcInstanceListLoader.PreviewFinish += _ =>
            Dispatcher.UIThread.Post(RefreshInstanceList);
        // 进入页面即触发一次加载（对齐 WPF RefreshOnNavigate）
        Loading.LoaderFolderRun(InstanceList.mcInstanceListLoader, GameFolders.mcFolderSelected,
            Loading.LoaderFolderRunType.RunOnUpdated, 1, "versions" + System.IO.Path.DirectorySeparatorChar);
        RefreshInstanceList();
    }

    /// <summary>重建实例列表 UI（对齐 WPF McInstanceListUI）。</summary>
    public void RefreshInstanceList()
    {
        try
        {
            var loader = InstanceList.mcInstanceListLoader;
            _panMain.Children.Clear();

            // 加载中 / 失败视图
            if (loader.State is LoadState.Waiting or LoadState.Loading)
            {
                _panLoad.IsVisible = true;
                _panEmpty.IsVisible = false;
                _panEmptySearch.IsVisible = false;
                _scroll.IsVisible = false;
                _searchBox.IsVisible = false;
                return;
            }

            _panLoad.IsVisible = false;

            var hasAnyResults = false;
            var hasAnyInstances = InstanceList.mcInstanceList.Values.Any(c => c.Count > 0);

            foreach (var card in InstanceList.mcInstanceList.ToArray())
            {
                if (card.Key == McInstanceCardType.Hidden) continue;
                var filtered = card.Value
                    .Where(v => _MatchesSearch(v, _searchText))
                    .ToList();
                if (filtered.Count == 0) continue;

                hasAnyResults = true;
                var cardNameKey = card.Key switch
                {
                    McInstanceCardType.OriginalLike => "Select.Instance.Card.Regular",
                    McInstanceCardType.API => _GetApiCardName(card.Value),
                    McInstanceCardType.Error => "Select.Instance.Card.Error",
                    McInstanceCardType.Rubbish => "Select.Instance.Card.LessUsed",
                    McInstanceCardType.Star => "Select.Instance.Card.Favorites",
                    McInstanceCardType.Fool => "Select.Instance.Card.AprilFools",
                    _ => "Select.Instance.Card.Modable",
                };

                var newCard = new MyCard
                {
                    Margin = new Thickness(0, 0, 0, 15),
                    Title = $"{Lang.Text(cardNameKey)} ({filtered.Count})",
                    CanSwap = true,
                    IsSwapped = card.Key is McInstanceCardType.Rubbish or McInstanceCardType.Error
                        or McInstanceCardType.Fool,
                };
                var stack = new StackPanel { Margin = new Thickness(20, 0, 18, 0) };
                foreach (var instance in filtered) stack.Children.Add(_BuildInstanceItem(instance));
                newCard.Content = stack;
                _panMain.Children.Add(newCard);
            }

            // 只有一个卡片时强制展开
            if (_panMain.Children.Count == 1 && _panMain.Children[0] is MyCard only && only.IsSwapped)
                only.IsSwapped = false;

            // 空状态判定
            _scroll.IsVisible = hasAnyResults;
            _searchBox.IsVisible = hasAnyInstances;
            _panEmpty.IsVisible = !hasAnyInstances;
            _panEmptySearch.IsVisible = hasAnyInstances && !hasAnyResults;
            if (!hasAnyResults)
            {
                if (!hasAnyInstances && _panEmpty.Content is StackPanel { } panel &&
                    panel.Children[0] is TextBlock title &&
                    panel.Children[2] is TextBlock content)
                {
                    title.Text = Lang.Text("Select.Instance.Empty.Title");
                    content.Text = Lang.Text("Select.Instance.Empty.Message");
                }
                else if (_panEmptySearch.Content is StackPanel { } searchPanel &&
                         searchPanel.Children[0] is TextBlock searchTitle &&
                         searchPanel.Children[2] is TextBlock searchContent)
                {
                    searchTitle.Text = Lang.Text("Select.Instance.EmptySearch.Title");
                    searchContent.Text = string.IsNullOrWhiteSpace(_searchText)
                        ? Lang.Text("Select.Instance.Search.EmptyInput")
                        : Lang.Text("Select.Instance.Search.NoResult", _searchBox.Text ?? _searchText);
                }
            }
        }
        catch (Exception ex)
        {
            global::PCL.Core.Logging.LogWrapper.Error(ex, "构建实例列表 UI 出错");
        }
    }

    private static string _GetApiCardName(List<McInstance> instances)
    {
        var isForge = false;
        var isNeoForge = false;
        var isFabric = false;
        var isQuilt = false;
        var isLite = false;
        var isCleanroom = false;
        var isLabyMod = false;
        foreach (var instance in instances)
        {
            if (!instance.IsLoaded) instance.Load();
            if (instance.Info.HasFabric) isFabric = true;
            if (instance.Info.HasQuilt) isQuilt = true;
            if (instance.Info.HasLiteLoader) isLite = true;
            if (instance.Info.HasForge) isForge = true;
            if (instance.Info.HasNeoForge) isNeoForge = true;
            if (instance.Info.HasCleanroom) isCleanroom = true;
            if (instance.Info.HasLabyMod) isLabyMod = true;
        }

        var count = new[] { isLite, isForge, isFabric, isNeoForge, isQuilt, isCleanroom, isLabyMod }
            .Count(b => b);
        if (count > 1) return "Select.Instance.Card.Modable";
        if (isForge) return "Select.Instance.Card.Forge";
        if (isNeoForge) return "Select.Instance.Card.NeoForge";
        if (isCleanroom) return "Select.Instance.Card.Cleanroom";
        if (isLabyMod) return "Select.Instance.Card.LabyMod";
        if (isLite) return "Select.Instance.Card.LiteLoader";
        if (isQuilt) return "Select.Instance.Card.Quilt";
        return "Select.Instance.Card.Fabric";
    }

    /// <summary>搜索过滤（对齐 PageSelectRightViewModel.MatchesSearch）。</summary>
    private static bool _MatchesSearch(McInstance instance, string search)
    {
        if (string.IsNullOrEmpty(search)) return true;
        string name, desc;
        try
        {
            name = instance.Name.ToLower();
            desc = (instance.Desc ?? "").ToLower();
        }
        catch
        {
            return false;
        }

        return name.Contains(search) || desc.Contains(search);
    }

    /// <summary>构建实例列表项（对齐 WPF McVersionListItem：高 42、Logo、标签、右侧按钮组）。</summary>
    private MyListItem _BuildInstanceItem(McInstance instance)
    {
        var item = new MyListItem
        {
            Title = instance.Name,
            Height = 42,
            Tag = instance,
            Type = MyListItemCheckType.Clickable,
        };
        try
        {
            item.Info = instance.Desc;
        }
        catch
        {
            // Desc 构建可能失败（损坏实例）
        }

        var info = instance.Info;
        var tags = new List<string> { McInstanceInfo.GetVanillaNameDisplay(info.VanillaName) };
        if (info.HasForge) tags.Add("Forge" + McInstanceInfo.FormatVersion(info.Forge));
        else if (info.HasNeoForge) tags.Add("NeoForge" + McInstanceInfo.FormatVersion(info.NeoForge));
        else if (info.HasCleanroom) tags.Add("Cleanroom" + McInstanceInfo.FormatVersion(info.Cleanroom));
        else if (info.HasLabyMod) tags.Add("LabyMod" + McInstanceInfo.FormatVersion(info.LabyMod));
        else if (info.HasQuilt) tags.Add("Quilt" + McInstanceInfo.FormatVersion(info.Quilt));
        else if (info.HasFabric) tags.Add("Fabric" + McInstanceInfo.FormatVersion(info.Fabric));
        if (info.HasLiteLoader) tags.Add("LiteLoader");
        if (info.HasOptiFine) tags.Add("OptiFine" + McInstanceInfo.FormatVersion(info.OptiFine));
        item.PlainTags = tags;

        try
        {
            item.Logo = instance.Logo;
        }
        catch
        {
            // Logo 缺失：不渲染
        }

        // 选中实例（主点击）
        item.Click += (_, _) =>
        {
            if (new McInstance(instance.PathInstance).Check())
            {
                InstanceList.McMcInstanceSelected = instance;
                global::PCL.Core.Logging.LogWrapper.Info("[Minecraft] 选择实例：" + instance.Name);
                AppUi.NavigateToLaunchHome();
            }
            else
            {
                try
                {
                    PCL.Core.App.Basics.OpenPath(instance.PathInstance);
                }
                catch
                {
                    // 忽略打开失败
                }
            }
        };

        // 右侧按钮：收藏 / 打开文件夹 / 删除（占位）/ 设置（占位）
        var btnStar = new MyIconButton
        {
            SvgIcon = instance.IsStar ? "lucide/heart-filled" : "lucide/heart",
            LogoScale = 1.1,
        };
        ToolTip.SetTip(btnStar, Lang.Text(instance.IsStar
            ? "Select.Instance.Unfavorite" : "Select.Instance.Favorite"));
        btnStar.Click += (_, _) =>
        {
            instance.IsStar = !instance.IsStar;
            PCL.Core.App.States.Instance.Starred[instance.PathInstance] = instance.IsStar;
            InstanceList.mcInstanceListForceRefresh = true;
            Loading.LoaderFolderRun(InstanceList.mcInstanceListLoader, GameFolders.mcFolderSelected,
                Loading.LoaderFolderRunType.ForceRun, 1, "versions" + System.IO.Path.DirectorySeparatorChar);
        };

        var btnOpen = new MyIconButton { SvgIcon = "lucide/folder-open", LogoScale = 1.1 };
        ToolTip.SetTip(btnOpen, Lang.Text("Select.Instance.OpenFolder"));
        btnOpen.Click += (_, _) =>
        {
            try
            {
                PCL.Core.App.Basics.OpenPath(instance.PathInstance);
            }
            catch
            {
                // 忽略打开失败
            }
        };

        var btnDelete = new MyIconButton { SvgIcon = "lucide/trash-2", LogoScale = 1.1 };
        ToolTip.SetTip(btnDelete, Lang.Text("Common.Action.Delete"));
        btnDelete.Click += (_, _) =>
            global::PCL.Core.UI.Hints.HintService.Show(new()
            {
                Message = "删除实例将在后续版本提供（MVP 占位）",
                Kind = global::PCL.Core.UI.Hints.HintKind.Info,
            });

        var btnSettings = new MyIconButton { SvgIcon = "lucide/settings", LogoScale = 1.1 };
        ToolTip.SetTip(btnSettings, Lang.Text("Select.Instance.Settings"));
        btnSettings.Click += (_, _) =>
            global::PCL.Core.UI.Hints.HintService.Show(new()
            {
                Message = "实例设置将在后续版本提供（MVP 占位）",
                Kind = global::PCL.Core.UI.Hints.HintKind.Info,
            });

        item.Buttons = instance.state != McInstanceState.Error
            ? [btnStar, btnOpen, btnDelete, btnSettings]
            : [btnStar, btnOpen, btnDelete];
        return item;
    }
}
