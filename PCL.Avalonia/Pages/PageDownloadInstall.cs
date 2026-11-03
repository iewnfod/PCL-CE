using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using PCL.Avalonia.Controls;
using PCL.Core.App;
using PCL.Core.App.Localization;
using PCL.Core.Minecraft;
using PCL.Core.UI.Hints;
using PCL.Core.Utils;
using System.Text.Json.Nodes;

namespace PCL.Avalonia.Pages;

/// <summary>
///     版本安装页（复刻 WPF PageDownloadInstall 的 MVP 子集）。<br/>
///     屏 1：原版版本清单（最新版本 + Release/Snapshot/旧版本/愚人节分组卡片）；<br/>
///     屏 2：加载器选择（Fabric / Forge / NeoForge + 自动 Fabric API）+ 实例名 + 安装按钮 + 安装进度。
/// </summary>
public class PageDownloadInstall : Grid
{
    // 屏 1：版本清单
    private readonly ScrollViewer _scrollMinecraft = new()
    {
        VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
        HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
    };

    private readonly StackPanel _panMinecraft = new() { Margin = new Thickness(25, 25, 25, 25) };
    private readonly MyCard _cardLoad = new() { Margin = new Thickness(0, 0, 0, 15) };

    // 屏 2：选择加载器
    private readonly ScrollViewer _scrollSelect = new()
    {
        VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
        HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
        IsVisible = false,
    };

    private readonly StackPanel _panSelect = new() { Margin = new Thickness(25, 25, 25, 25) };

    // 选择状态
    private string? _vanillaName;
    private JsonObject? _vanillaData;
    private string? _selectedFabric;
    private GameDownload.DlForgeVersionEntry? _selectedForge;
    private GameDownload.DlNeoForgeListEntry? _selectedNeoForge;
    private Comp.CompFile? _selectedFabricApi;
    private bool _autoSelectedFabricApi;

    private TextBox _txtName = null!;
    private TextBlock _labNameError = null!;
    private MyButton _btnStart = null!;
    private MyCard _cardProgress = null!;
    private ProgressBar _progressBar = null!;
    private TextBlock _labProgress = null!;
    private MyCard _cardFabric = null!;
    private MyCard _cardForge = null!;
    private MyCard _cardNeoForge = null!;
    private TextBlock _labFabricApi = null!;

    private Loading.LoaderTask<string, List<GameDownload.DlForgeVersionEntry>>? _forgeLoader;
    private Loading.LoaderCombo<string>? _activeInstall;
    private readonly DispatcherTimer _progressTimer;
    private bool _loaderHooked;

    public PageDownloadInstall()
    {
        _progressTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
        _progressTimer.Tick += (_, _) => _OnProgressTimerTick();

        Children.Add(_scrollMinecraft);
        Children.Add(_scrollSelect);
        _scrollMinecraft.Content = _panMinecraft;
        _scrollSelect.Content = _panSelect;

        _cardLoad.Content = new MyLoading
        {
            Margin = new Thickness(20, 0, 20, 17),
            IsHitTestVisible = false,
        };
        _panMinecraft.Children.Add(_cardLoad);

        _BuildSelectPage();

        // 版本清单加载
        GameDownload.dlClientListLoader.Start();
        GameDownload.DlFabricListLoader.Start();
        GameDownload.DlNeoForgeListLoader.Start();
        GameDownload.DlOptiFineListLoader.Start();
        CompDownloadLoaders.DlFabricApiLoader.Start();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (_loaderHooked) return;
        _loaderHooked = true;
        GameDownload.dlClientListLoader.PreviewFinish += _ => Dispatcher.UIThread.Post(RebuildVersionCards);
        GameDownload.DlFabricListLoader.PreviewFinish += _ => Dispatcher.UIThread.Post(() => _RefreshFabricCard());
        GameDownload.DlNeoForgeListLoader.PreviewFinish += _ => Dispatcher.UIThread.Post(() => _RefreshNeoForgeCard());
        CompDownloadLoaders.DlFabricApiLoader.PreviewFinish += _ => Dispatcher.UIThread.Post(_AutoSelectFabricApi);
        if (_forgeLoader is not null)
            _forgeLoader.PreviewFinish += _ => Dispatcher.UIThread.Post(() => _RefreshForgeCard());
        RebuildVersionCards();
    }

    #region 自动化验证钩子（PCL_SELECT_VERSION / PCL_AUTO_INSTALL，与 PCL_NAV 模式一致）

    private static string? AutoSelectVersion => Environment.GetEnvironmentVariable("PCL_SELECT_VERSION");
    private static bool AutoInstall => Environment.GetEnvironmentVariable("PCL_AUTO_INSTALL") == "1";
    private bool _autoSelectDone;

    /// <summary>版本清单就绪后自动选择指定版本（自动化验证用）。</summary>
    private void _RunAutoSelectHook()
    {
        if (_autoSelectDone || AutoSelectVersion is null || _isInSelectPage) return;
        if (GameDownload.dlClientListLoader.output.Value?["versions"] is not JsonArray versions) return;
        foreach (JsonObject version in versions)
            if (version["id"]?.ToString() == AutoSelectVersion)
            {
                _autoSelectDone = true;
                LauncherCommon.Log($"[Download] 自动化钩子：选择版本 {AutoSelectVersion}");
                MinecraftSelected(AutoSelectVersion, version);
                _RunAutoInstallHook();
                break;
            }
    }

    /// <summary>进入选择屏后，等待各加载器清单就绪再自动开始安装（自动化验证用）。</summary>
    private void _RunAutoInstallHook()
    {
        if (!AutoInstall) return;
        _ = Task.Run(async () =>
        {
            // 等待 Fabric / NeoForge 清单与 Forge 子清单结束（最多 60s）
            for (var i = 0; i < 600; i++)
            {
                var fabricDone = GameDownload.DlFabricListLoader.State
                    is LoadState.Finished or LoadState.Failed;
                var neoDone = GameDownload.DlNeoForgeListLoader.State
                    is LoadState.Finished or LoadState.Failed;
                var forgeDone = _forgeLoader is null || _forgeLoader.State
                    is LoadState.Finished or LoadState.Failed;
                if (fabricDone && neoDone && forgeDone) break;
                await Task.Delay(100);
            }

            // 自动选择加载器（PCL_SELECT_LOADER=fabric|neoforge，选最新稳定版）
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                switch (Environment.GetEnvironmentVariable("PCL_SELECT_LOADER"))
                {
                    case "fabric":
                        if (GameDownload.DlFabricListLoader.State == LoadState.Finished
                            && GameDownload.DlFabricListLoader.output.Value?["loader"] is JsonArray fabricVersions)
                            foreach (JsonObject entry in fabricVersions)
                                if (entry["stable"].ToObject<bool>())
                                {
                                    FabricSelected(entry["version"]?.ToString() ?? "");
                                    break;
                                }
                        break;
                    case "neoforge":
                        if (GameDownload.DlNeoForgeListLoader.State == LoadState.Finished)
                        {
                            var neo = GameDownload.DlNeoForgeListLoader.output.Value
                                .Where(v => (v.Inherit ?? "") == (_vanillaName ?? "") && !v.IsBeta)
                                .OrderByDescending(v => v)
                                .FirstOrDefault();
                            if (neo is not null) NeoForgeSelected(neo);
                        }
                        break;
                }
            });

            await Task.Delay(500); // 等 Fabric API 自动选择落地
            await Dispatcher.UIThread.InvokeAsync(async () =>
            {
                LauncherCommon.Log($"[Download] 自动化钩子：开始安装（实例名 {_txtName.Text}）");
                await BtnStart_Click();
            });
        });
    }

    #endregion

    #region 屏 1：版本清单

    /// <summary>重建版本分组卡片（对齐 WPF _RebuildVersionCards）。</summary>
    public void RebuildVersionCards()
    {
        try
        {
            _panMinecraft.Children.Clear();

            var loader = GameDownload.dlClientListLoader;
            if (loader.State is LoadState.Waiting or LoadState.Loading)
            {
                _panMinecraft.Children.Add(_cardLoad);
                return;
            }

            if (loader.State == LoadState.Failed)
            {
                var failCard = new MyCard
                {
                    Margin = new Thickness(0, 0, 0, 15),
                    Title = Lang.Text("Download.Install.State.GetFailed", loader.Error.Message),
                };
                failCard.Content = new StackPanel
                {
                    Margin = new Thickness(20, 4, 20, 18),
                    Children =
                    {
                        new MyButton
                        {
                            Text = Lang.Text("Common.Action.Refresh"),
                            Height = 32,
                            MinWidth = 100,
                            HorizontalAlignment = HorizontalAlignment.Left,
                        },
                    },
                };
                ((StackPanel)failCard.Content).Children.OfType<MyButton>().First().Click +=
                    (_, _) => GameDownload.dlClientListLoader.Start(0, true);
                _panMinecraft.Children.Add(failCard);
                return;
            }

            if (loader.output.Value?["versions"] is not JsonArray versions)
                return;

            var categoryOrder = new[]
            {
                McVersionCategory.Release,
                McVersionCategory.Snapshot,
                McVersionCategory.BeforeRelease,
                McVersionCategory.AprilFools
            };
            var dict = categoryOrder.ToDictionary(category => category, _ => new List<JsonObject>());
            foreach (JsonObject version in versions)
                dict[McVersionClassifier.ClassifyVersion(version)].Add(version);
            foreach (var category in categoryOrder)
                dict[category] = dict[category]
                    .OrderByDescending(McVersionClassifier.GetReleaseTime)
                    .ToList();

            _AddLatestVersionCard(dict);
            foreach (var category in categoryOrder)
            {
                var list = dict[category];
                if (list.Count == 0) continue;
                _AddVersionCategoryCard(
                    $"{Lang.Text(McVersionClassifier.GetCategoryKey(category))} ({list.Count})",
                    list,
                    defaultSwapped: true); // 对照 WPF：分组卡默认全部折叠
            }

            _RunAutoSelectHook();
        }
        catch (Exception ex)
        {
            global::PCL.Core.Logging.LogWrapper.Error(ex, "构建版本清单 UI 出错");
        }
    }

    private void _AddLatestVersionCard(Dictionary<McVersionCategory, List<JsonObject>> dict)
    {
        var latestRelease = dict[McVersionCategory.Release].FirstOrDefault();
        var latestSnapshot = dict[McVersionCategory.Snapshot].FirstOrDefault();

        var latestVersions = new List<JsonObject>();
        if (latestRelease is not null)
        {
            var release = (JsonObject)latestRelease.DeepClone();
            release["lore"] = "Download.Version.Latest.Release";
            release["loreArgs"] = new JsonArray(
                McVersionClassifier.GetReleaseTime(release)
                    .ToString("O", System.Globalization.CultureInfo.InvariantCulture));
            release["loreArgsType"] = "date";
            latestVersions.Add(release);
        }

        if (latestSnapshot is not null &&
            (latestRelease is null || McVersionClassifier.GetReleaseTime(latestRelease) <
                McVersionClassifier.GetReleaseTime(latestSnapshot)))
        {
            var snapshot = (JsonObject)latestSnapshot.DeepClone();
            snapshot["lore"] = "Download.Version.Latest.Development";
            snapshot["loreArgs"] = new JsonArray(
                McVersionClassifier.GetReleaseTime(snapshot)
                    .ToString("O", System.Globalization.CultureInfo.InvariantCulture));
            snapshot["loreArgsType"] = "date";
            latestVersions.Add(snapshot);
        }

        if (latestVersions.Count == 0)
            return;

        _AddVersionCategoryCard(Lang.Text("Download.Version.Latest.Title"), latestVersions, false);
    }

    private void _AddVersionCategoryCard(string title, List<JsonObject> versions, bool defaultSwapped)
    {
        var card = new MyCard
        {
            Margin = new Thickness(0, 0, 0, 15),
            Title = title,
            CanSwap = true,
            IsSwapped = defaultSwapped,
        };

        void Fill(MyCard c)
        {
            if (c.Content is not null) return;
            var stack = new StackPanel { Margin = new Thickness(20, 0, 18, 15) };
            foreach (var version in versions)
                stack.Children.Add(_BuildVersionItem(version));
            c.Content = stack;
        }

        if (!defaultSwapped)
            Fill(card);
        else
            card.Swap += (_, _) =>
            {
                if (!card.IsSwapped) Fill(card); // 展开：按需填充（懒构建，700+ 版本不全量实例化）
            };

        _panMinecraft.Children.Add(card);
    }

    /// <summary>构建单个版本列表项（对齐 DownloadUi.McDownloadListItem）。</summary>
    private MyListItem _BuildVersionItem(JsonObject entry)
    {
        var versionId = entry["id"]?.ToString() ?? "";
        var formattedVersion = McFormatter.FormatVersion(versionId).Replace("_", " ");
        var item = new MyListItem
        {
            Title = formattedVersion,
            Height = 42,
            Type = MyListItemCheckType.Clickable,
            Tag = entry,
            SvgIcon = (entry["type"]?.ToString() ?? "") switch
            {
                "release" => "lucide/box",
                "snapshot" or "pending" => "lucide/flask-conical",
                "special" => "lucide/sparkles",
                _ => "lucide/boxes",
            },
            LogoScale = 0.95,
        };

        string info;
        if (entry["lore"] is null)
        {
            var releaseTime = entry["releaseTime"].ToObject<DateTime>();
            info = formattedVersion != versionId
                ? $"{Lang.Date(releaseTime, "g")} | {versionId}"
                : Lang.Date(releaseTime, "g");
        }
        else
        {
            var loreDate = default(DateTime);
            var isDate = DateTime.TryParse(
                ((JsonArray?)entry["loreArgs"])?.FirstOrDefault()?.ToString(),
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.RoundtripKind, out loreDate);
            var lore = isDate
                ? Lang.Text(entry["lore"].ToString(), Lang.Date(loreDate, "g"))
                : Lang.Text(entry["lore"].ToString());
            info = formattedVersion != versionId ? $"{lore} | {versionId}" : lore;
        }

        item.Info = info;
        if ((entry["url"]?.ToString() ?? "").Contains("unlisted-versions-of-minecraft"))
            item.PlainTags = [Lang.Text("Download.Source.Tag.Uvmc")];

        item.Click += (_, _) => MinecraftSelected(versionId, entry);
        return item;
    }

    #endregion

    #region 屏 2：选择加载器

    private void _BuildSelectPage()
    {
        // 顶部卡：返回按钮 + 实例图标 + 实例名输入框（对照 WPF 参考截图）
        var topCard = new MyCard { Margin = new Thickness(0, 0, 0, 15) };
        var btnBack = new MyIconButton { SvgIcon = "lucide/arrow-left" };
        ToolTip.SetTip(btnBack, Lang.Text("Download.Install.Hint.MinecraftBack"));
        btnBack.Click += (_, _) => ExitSelectPage();
        var logo = new SvgIcon
        {
            Icon = "lucide/box",
            Width = 24,
            Height = 24,
            VerticalAlignment = VerticalAlignment.Center,
        };
        logo.SetDynamic(SvgIcon.IconBrushProperty, "ColorBrush1");
        _txtName = new TextBox
        {
            FontSize = 14,
            MinHeight = 32,
            VerticalAlignment = VerticalAlignment.Center,
            CornerRadius = new CornerRadius(4),
        };
        _txtName.SetDynamic(TextBox.BorderBrushProperty, "ColorBrushGray4");
        _txtName.TextChanged += (_, _) =>
        {
            if (_isNameChanging) return;
            // Avalonia 的 TextChanged 可能异步于 Text 赋值触发（_isNameChanging 已复位），
            // 故再用值比较兜底：程序刚写入的期望值不算用户编辑
            if (_txtName.Text == GetSelectName()) return;
            _isNameEdited = true;
            _ValidateName();
        };
        var topRow = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,Auto,*,Auto") };
        topRow.Children.Add(btnBack);
        Grid.SetColumn(logo, 1);
        logo.Margin = new Thickness(12, 0, 4, 0);
        topRow.Children.Add(logo);
        Grid.SetColumn(_txtName, 2);
        topRow.Children.Add(_txtName);
        _labNameError = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            FontSize = 12,
            Margin = new Thickness(40, 4, 12, 0),
            IsVisible = false,
        };
        _labNameError.SetDynamic(TextBlock.ForegroundProperty, "ColorBrushRed");
        var topStack = new StackPanel { Margin = new Thickness(14, 6, 12, 12) };
        topStack.Children.Add(topRow);
        topStack.Children.Add(_labNameError);
        topCard.Content = topStack;
        _panSelect.Children.Add(topCard);

        _cardFabric = _BuildLoaderCard("Fabric");
        _cardForge = _BuildLoaderCard("Forge");
        _cardNeoForge = _BuildLoaderCard("NeoForge");
        _panSelect.Children.Add(_cardFabric);
        _panSelect.Children.Add(_cardForge);
        _panSelect.Children.Add(_cardNeoForge);

        // Fabric API 自动选择提示
        _labFabricApi = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            Opacity = 0.75,
            FontSize = 12,
            Margin = new Thickness(2, 0, 2, 12),
            IsVisible = false,
        };
        _panSelect.Children.Add(_labFabricApi);

        // 安装进度卡
        _progressBar = new ProgressBar
        {
            Height = 6,
            Minimum = 0,
            Maximum = 1,
        };
        _progressBar.SetDynamic(ProgressBar.ForegroundProperty, "ColorBrushHighlight");
        _labProgress = new TextBlock
        {
            FontSize = 12,
            Margin = new Thickness(0, 8, 0, 0),
            TextWrapping = TextWrapping.Wrap,
        };
        _cardProgress = new MyCard
        {
            Title = "安装进度",
            Margin = new Thickness(0, 0, 0, 15),
            IsVisible = false,
        };
        _cardProgress.Content = new StackPanel
        {
            Margin = new Thickness(20, 4, 20, 16),
            Children = { _progressBar, _labProgress },
        };
        _panSelect.Children.Add(_cardProgress);

        // 安装按钮（底部居中胶囊，对照 WPF 参考截图）
        _btnStart = new MyButton
        {
            Text = Lang.Text("Download.Install.StartDownload"),
            Height = 38,
            MinWidth = 180,
            Margin = new Thickness(0, 4, 0, 12),
            HorizontalAlignment = HorizontalAlignment.Center,
            ColorType = MyButtonColorType.Highlight,
        };
        _btnStart.Click += (_, _) => _ = BtnStart_Click();
        _panSelect.Children.Add(_btnStart);
    }

    private MyCard _BuildLoaderCard(string name)
    {
        var card = new MyCard
        {
            Title = _LoaderTitle(name, null),
            Margin = new Thickness(0, 0, 0, 15),
            CanSwap = true,
            IsSwapped = true,
        };
        // 展开：填充版本列表
        card.Swap += (_, _) =>
        {
            if (!card.IsSwapped)
                _FillLoaderCard(card, name);
        };
        return card;
    }

    /// <summary>加载器卡折叠态标题：已选版本 / 可以添加 / 无可用版本（对照 WPF 卡片状态文字）。</summary>
    private static string _LoaderTitle(string name, bool? hasVersions)
    {
        return hasVersions is null
            ? $"{name} · {Lang.Text("Download.Install.State.Loading")}"
            : hasVersions.Value
                ? $"{name} · {Lang.Text("Download.Install.State.CanAdd")}"
                : $"{name} · {Lang.Text("Download.Install.State.NoVersion")}";
    }

    private void _FillLoaderCard(MyCard card, string name)
    {
        switch (name)
        {
            case "Fabric":
                _RefreshFabricCard();
                break;
            case "Forge":
                _RefreshForgeCard();
                break;
            case "NeoForge":
                _RefreshNeoForgeCard();
                break;
        }
    }

    private void _RefreshFabricCard()
    {
        var loader = GameDownload.DlFabricListLoader;
        if (loader.State == LoadState.Finished && loader.output.Value?["loader"] is JsonArray versions)
        {
            if (_selectedFabric is null)
                _cardFabric.Title = _LoaderTitle("Fabric", versions.Count > 0);
            var stack = new StackPanel { Margin = new Thickness(20, 0, 18, 15) };
            foreach (JsonObject entry in versions)
            {
                var version = entry["version"]?.ToString() ?? "";
                var item = new MyListItem
                {
                    Title = version.Replace("+build", ""),
                    Info = entry["stable"].ToObject<bool>()
                        ? Lang.Text("Download.Version.Type.Stable")
                        : Lang.Text("Download.Version.Type.Preview"),
                    Height = 42,
                    Type = MyListItemCheckType.Clickable,
                    Tag = version,
                    SvgIcon = "lucide/package",
                    LogoScale = 0.95,
                    Checked = version == _selectedFabric,
                };
                item.Click += (_, _) => FabricSelected(version);
                stack.Children.Add(item);
            }

            _cardFabric.Content = stack;
        }
        else
        {
            _cardFabric.Title = loader.State == LoadState.Failed
                ? $"Fabric — {Lang.Text("Download.Install.State.GetFailed", loader.Error.Message)}"
                : "Fabric — 加载中…";
            _cardFabric.Content = new MyLoading
            {
                Margin = new Thickness(20, 0, 20, 17),
                IsHitTestVisible = false,
            };
        }
    }

    private void _RefreshForgeCard()
    {
        var loader = _forgeLoader;
        if (loader?.State != LoadState.Finished)
        {
            _cardForge.Title = loader?.State == LoadState.Failed
                ? $"Forge — {Lang.Text("Download.Install.State.GetFailed", loader!.Error.Message)}"
                : "Forge — 加载中…";
            if (_cardForge.IsSwapped) return;
            _cardForge.Content = new MyLoading
            {
                Margin = new Thickness(20, 0, 20, 17),
                IsHitTestVisible = false,
            };
            return;
        }

        var versions = loader.output?
            .Where(v => v.Category != "universal" && v.Category != "client")
            .OrderByDescending(v => v)
            .ToList() ?? [];
        if (_selectedForge is null)
            _cardForge.Title = _LoaderTitle("Forge", versions.Count > 0);
        var stack = new StackPanel { Margin = new Thickness(20, 0, 18, 15) };
        foreach (var version in versions)
        {
            var item = new MyListItem
            {
                Title = version.version.Revision.ToString(System.Globalization.CultureInfo.InvariantCulture),
                Info = version.IsRecommended
                    ? Lang.Text("Download.Version.Type.Recommended")
                    : Lang.Text("Download.Version.Type.Preview"),
                Height = 42,
                Type = MyListItemCheckType.Clickable,
                Tag = version,
                SvgIcon = "lucide/anvil",
                LogoScale = 0.95,
                Checked = ReferenceEquals(version, _selectedForge),
            };
            item.Click += (_, _) => ForgeSelected(version);
            stack.Children.Add(item);
        }

        _cardForge.Content = stack;
    }

    private void _RefreshNeoForgeCard()
    {
        var loader = GameDownload.DlNeoForgeListLoader;
        if (loader.State == LoadState.Finished)
        {
            var versions = loader.output.Value
                .Where(v => (v.Inherit ?? "") == (_vanillaName ?? ""))
                .OrderByDescending(v => v)
                .ToList();
            if (versions.Count == 0)
            {
                _cardNeoForge.Title = _LoaderTitle("NeoForge", false);
                _cardNeoForge.Content = new TextBlock
                {
                    Text = Lang.Text("Download.Install.State.NoVersion"),
                    Opacity = 0.6,
                    Margin = new Thickness(20, 8, 20, 16),
                };
                return;
            }

            if (_selectedNeoForge is null)
                _cardNeoForge.Title = _LoaderTitle("NeoForge", true);
            var stack = new StackPanel { Margin = new Thickness(20, 0, 18, 15) };
            foreach (var version in versions)
            {
                var item = new MyListItem
                {
                    Title = version.VersionName,
                    Info = version.IsBeta
                        ? Lang.Text("Download.Version.Type.Preview")
                        : Lang.Text("Download.Version.Type.Stable"),
                    Height = 42,
                    Type = MyListItemCheckType.Clickable,
                    Tag = version,
                    SvgIcon = "lucide/anvil",
                    LogoScale = 0.95,
                    Checked = ReferenceEquals(version, _selectedNeoForge),
                };
                item.Click += (_, _) => NeoForgeSelected(version);
                stack.Children.Add(item);
            }

            _cardNeoForge.Content = stack;
        }
        else
        {
            _cardNeoForge.Title = loader.State == LoadState.Failed
                ? $"NeoForge — {Lang.Text("Download.Install.State.GetFailed", loader.Error.Message)}"
                : "NeoForge — 加载中…";
            _cardNeoForge.Content = new MyLoading
            {
                Margin = new Thickness(20, 0, 20, 17),
                IsHitTestVisible = false,
            };
        }
    }

    #endregion

    #region 页面切换与选择（对齐 WPF EnterSelectPage / ExitSelectPage / *_Selected）

    private bool _isInSelectPage;

    public void MinecraftSelected(string vanillaName, JsonObject vanillaData)
    {
        _vanillaName = vanillaName;
        _vanillaData = vanillaData;
        EnterSelectPage();
    }

    private void EnterSelectPage()
    {
        if (_isInSelectPage) return;
        _isInSelectPage = true;

        _selectedFabric = null;
        _selectedForge = null;
        _selectedNeoForge = null;
        _selectedFabricApi = null;
        _autoSelectedFabricApi = false;
        _isNameEdited = false;
        _labFabricApi.IsVisible = false;

        _cardFabric.Title = _LoaderTitle("Fabric", null);
        _cardFabric.IsSwapped = true;
        _cardFabric.Content = null;
        _cardForge.Title = _LoaderTitle("Forge", null);
        _cardForge.IsSwapped = true;
        _cardForge.Content = null;
        _cardNeoForge.Title = _LoaderTitle("NeoForge", null);
        _cardNeoForge.IsSwapped = true;
        _cardNeoForge.Content = null;

        _scrollMinecraft.IsVisible = false;
        _scrollSelect.IsVisible = true;
        _scrollSelect.Offset = new Vector(0, 0);

        // 启动 Forge 版本清单加载（对应 WPF EnterSelectPage）
        if (_vanillaName is not null && McInstanceInfo.IsFormatFit(_vanillaName))
        {
            _forgeLoader = new Loading.LoaderTask<string, List<GameDownload.DlForgeVersionEntry>>(
                "DlForgeVersion " + _vanillaName, GameDownload.DlForgeVersionMain);
            _forgeLoader.PreviewFinish += _ => Dispatcher.UIThread.Post(_RefreshForgeCard);
            _forgeLoader.Start(_vanillaName);
        }
        else
        {
            _forgeLoader = null;
            _cardForge.Title = _LoaderTitle("Forge", false);
            _cardForge.Content = new TextBlock
            {
                Text = Lang.Text("Download.Install.State.NoVersion"),
                Opacity = 0.6,
                Margin = new Thickness(20, 8, 20, 16),
            };
        }

        GameDownload.DlFabricListLoader.Start();
        GameDownload.DlNeoForgeListLoader.Start();
        CompDownloadLoaders.DlFabricApiLoader.Start();
        _RefreshFabricCard();
        _RefreshNeoForgeCard();
        _UpdateSelectName();
        _ValidateName();
    }

    public void ExitSelectPage()
    {
        if (!_isInSelectPage) return;
        _isInSelectPage = false;

        _scrollSelect.IsVisible = false;
        _scrollMinecraft.IsVisible = true;
        _scrollMinecraft.Offset = new Vector(0, 0);
    }

    private void FabricSelected(string version)
    {
        _selectedFabric = version;
        _selectedForge = null;
        _selectedNeoForge = null;
        _selectedFabricApi = null;
        _autoSelectedFabricApi = false;
        LauncherCommon.Log("[Download] 选择 Fabric：" + version);
        _cardFabric.Title = $"Fabric：{version.Replace("+build", "")}";
        _cardFabric.IsSwapped = true;
        _cardForge.Title = _LoaderTitle("Forge", true);
        _cardNeoForge.Title = _LoaderTitle("NeoForge", true);
        _AutoSelectFabricApi();
        _UpdateSelectName();
    }

    private void ForgeSelected(GameDownload.DlForgeVersionEntry version)
    {
        _selectedFabric = null;
        _selectedForge = version;
        _selectedNeoForge = null;
        _selectedFabricApi = null;
        LauncherCommon.Log("[Download] 选择 Forge：" + version.VersionName);
        _cardForge.Title = $"Forge：{version.VersionName}";
        _cardForge.IsSwapped = true;
        _cardFabric.Title = _LoaderTitle("Fabric", true);
        _cardNeoForge.Title = _LoaderTitle("NeoForge", true);
        _labFabricApi.IsVisible = false;
        _UpdateSelectName();
    }

    private void NeoForgeSelected(GameDownload.DlNeoForgeListEntry version)
    {
        _selectedFabric = null;
        _selectedForge = null;
        _selectedNeoForge = version;
        _selectedFabricApi = null;
        LauncherCommon.Log("[Download] 选择 NeoForge：" + version.VersionName);
        _cardNeoForge.Title = $"NeoForge：{version.VersionName}";
        _cardNeoForge.IsSwapped = true;
        _cardFabric.Title = _LoaderTitle("Fabric", true);
        _cardForge.Title = _LoaderTitle("Forge", true);
        _labFabricApi.IsVisible = false;
        _UpdateSelectName();
    }

    /// <summary>自动选择适配当前原版版本的最新 Fabric API（对齐 WPF FabricApi_Loaded 自动选择逻辑）。</summary>
    private void _AutoSelectFabricApi()
    {
        try
        {
            if (CompDownloadLoaders.DlFabricApiLoader.State != LoadState.Finished)
                return;
            if (_vanillaName is null || _selectedFabric is null)
                return;

            var targetName = _vanillaName
                .Replace("∞", "infinite")
                .Replace("Combat Test 7c", "1.16_combat-3")
                .ToLower();
            var versions = CompDownloadLoaders.DlFabricApiLoader.output
                .Where(f => f.RawGameVersions.Any(g => g == targetName))
                .OrderByDescending(f => f.ReleaseDate)
                .ToList();

            if (versions.Count == 0)
            {
                _selectedFabricApi = null;
                _labFabricApi.IsVisible = false;
                return;
            }

            if (!_autoSelectedFabricApi)
            {
                _autoSelectedFabricApi = true;
                _selectedFabricApi = versions[0];
                LauncherCommon.Log($"[Download] 已自动选择 Fabric API：{versions[0].DisplayName}");
            }

            if (_selectedFabricApi is not null)
            {
                _labFabricApi.Text = $"将自动安装兼容的 Fabric API：{_selectedFabricApi.DisplayName}";
                _labFabricApi.IsVisible = true;
            }
        }
        catch (Exception ex)
        {
            LauncherCommon.Log(ex, "自动选择 Fabric API 出错");
        }
    }

    #endregion

    #region 实例名与安装

    private bool _isNameEdited;

    /// <summary>获取默认实例名（对齐 WPF GetSelectName）。</summary>
    private string GetSelectName()
    {
        var name = _vanillaName;
        if (_selectedFabric is not null) name += "-Fabric_" + _selectedFabric.Replace("+build", "");
        if (_selectedForge is not null) name += "-Forge_" + _selectedForge.VersionName;
        if (_selectedNeoForge is not null) name += "-NeoForge_" + _selectedNeoForge.VersionName;
        return name;
    }

    private void _UpdateSelectName()
    {
        if (_isNameEdited || _isNameChanging) return;
        _isNameChanging = true;
        _txtName.Text = GetSelectName();
        _isNameChanging = false;
        _ValidateName();
    }

    private bool _isNameChanging;

    private void _ValidateName()
    {
        var name = _txtName.Text ?? "";
        string? error = null;
        if (!string.IsNullOrWhiteSpace(name) && !LauncherCommon.IsValidInstanceName(name))
            error = "实例名包含非法字符或过长（最多 64 字符）";
        else if (!string.IsNullOrWhiteSpace(name)
                 && File.Exists(System.IO.Path.Combine(
                     GameFolders.mcFolderSelected ?? "", "versions", name, $"{name}.json")))
            error = Lang.Text("Minecraft.Download.Error.InstanceAlreadyExists", name, "");

        _labNameError.Text = error;
        _labNameError.IsVisible = error is not null;
        _btnStart.IsEnabled = error is null && !string.IsNullOrWhiteSpace(name) && _activeInstall is null;
    }

    private async Task BtnStart_Click()
    {
        if (_vanillaName is null || _vanillaData is null) return;
        var instanceName = _txtName.Text;
        if (string.IsNullOrWhiteSpace(instanceName)) return;

        // 确认版本隔离（对齐 WPF BtnStart_Click）
        var hasLoader = _selectedFabric is not null || _selectedForge is not null || _selectedNeoForge is not null;
        if (hasLoader && (Config.Launch.IndieSolutionV2 == 0 || Config.Launch.IndieSolutionV2 == 2))
        {
            var choice = await MyMsgBox.ShowAsync(
                Lang.Text("Download.Install.InstanceIsolation.Warning.Message"),
                Lang.Text("Download.Install.InstanceIsolation.Warning.Title"),
                [
                    Lang.Text("Download.Install.InstanceIsolation.Warning.Cancel"),
                    Lang.Text("Download.Install.InstanceIsolation.Warning.Continue"),
                ]);
            // 按钮序号 1 起：1 = 继续安装，2 = 取消（与 WPF 版按钮顺序一致）
            if (choice != 1) return;
        }

        var request = new McInstallRequest
        {
            targetInstanceName = instanceName,
            targetInstanceFolder = DownloadUi.GetInstanceFolderPath(instanceName),
            minecraftJson = _vanillaData["url"]?.ToString(),
            minecraftName = _vanillaName,
            forgeEntry = _selectedForge,
            neoForgeEntry = _selectedNeoForge,
            fabricVersion = _selectedFabric,
            fabricApi = _selectedFabricApi,
        };

        // 轮询进度：跟踪本次安装的 LoaderCombo（加入任务栏的最新同名任务）
        Loading.LoaderBase? tracked = null;
        foreach (var task in Loading.loaderTaskbar.ToList())
            if (task.name == $"{instanceName} 安装")
                tracked = task;
        _activeInstall = tracked as Loading.LoaderCombo<string>;

        // McInstall 在后台线程执行：其内部日志可能触发全局错误弹窗，在 UI 线程同步等待会死锁
        var started = await Task.Run(() => DownloadUi.McInstall(request));
        if (!started)
        {
            _activeInstall = null;
            _ValidateName();
            return;
        }

        // 找到刚启动的任务（McInstall 内部创建）
        if (_activeInstall is null)
        {
            foreach (var task in Loading.loaderTaskbar.ToList())
                if (task.name == $"{instanceName} 安装")
                    _activeInstall = task as Loading.LoaderCombo<string>;
        }

        _cardProgress.IsVisible = true;
        _progressBar.Value = 0;
        _labProgress.Text = request.targetInstanceName;
        _ValidateName();
        _progressTimer.Start();

        ExitSelectPage();
    }

    private void _OnProgressTimerTick()
    {
        if (_activeInstall is null)
        {
            _StopProgressTracking();
            return;
        }

        // 完成态保留展示 5 秒后隐藏进度卡
        if (_activeInstall.State is LoadState.Finished or LoadState.Failed or LoadState.Aborted)
        {
            if (_installFinishedAt is null)
                _installFinishedAt = Environment.TickCount64;
            else if (Environment.TickCount64 - _installFinishedAt > 5000)
            {
                _StopProgressTracking();
                return;
            }
        }

        _UpdateInstallProgress();
    }

    private long? _installFinishedAt;

    private void _UpdateInstallProgress()
    {
        var loader = _activeInstall;
        if (loader is null) return;

        _progressBar.Value = Math.Clamp(loader.Progress, 0, 1);

        // 当前正在运行的可见子阶段名
        string? stage = null;
        try
        {
            foreach (var sub in loader.GetLoaderList())
                if (sub.State == LoadState.Loading)
                {
                    stage = sub.name;
                    break;
                }
        }
        catch
        {
            // GetLoaderList 仅在特定状态下可用
        }

        _labProgress.Text = loader.State switch
        {
            LoadState.Finished => $"{loader.name}{Lang.Text("Common.Status.Success")}",
            LoadState.Failed => $"{loader.name}{Lang.Text("Common.Status.Failure")}{loader.Error.Message}",
            LoadState.Aborted => $"{loader.name}{Lang.Text("Common.Status.Cancelled")}",
            _ => $"{loader.name} — {stage ?? ""} ({Math.Floor(loader.Progress * 100)}%)",
        };
    }

    private void _StopProgressTracking()
    {
        _progressTimer.Stop();
        _cardProgress.IsVisible = false;
        _activeInstall = null;
        _installFinishedAt = null;
        _ValidateName();
    }

    #endregion
}
