using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using PCL.Avalonia.Controls;
using PCL.Avalonia.Platform;
using PCL.Core.App;
using PCL.Core.App.Localization;
using PCL.Core.Utils.OS;

namespace PCL.Avalonia.Pages;

/// <summary>
///     设置-启动页（复刻 WPF PageSetupLaunch 的 MVP 子集）：
///     启动选项（启动器可见性）+ 游戏内存（自动/手动、-Xmx/-Xms 滑块、内存可视化）+ 启动器行为（提示/锁定窗口）。
/// </summary>
public class PageSetupLaunch : Grid
{
    private readonly MyComboBox _comboVisibility = new();
    private readonly MyRadioButton _radioAuto = new()
    {
        FontSize = 13,
        ColorType = MyRadioButtonColorType.RadioBox,
    };
    private readonly MyRadioButton _radioCustom = new()
    {
        FontSize = 13,
        ColorType = MyRadioButtonColorType.RadioBox,
    };
    private readonly MySlider _sliderMax = new() { MaxValue = 49 };
    private readonly MySlider _sliderInitial = new() { MaxValue = 49 };
    private readonly TextBlock _labMaxValue = new();
    private readonly TextBlock _labInitialValue = new();
    private readonly Rectangle _rectUsed = new();
    private readonly Rectangle _rectGame = new();
    private readonly ColumnDefinition _colUsed = new(4.7, GridUnitType.Star);
    private readonly ColumnDefinition _colGame = new(2.5, GridUnitType.Star);
    private readonly ColumnDefinition _colEmpty = new(0.7, GridUnitType.Star);
    private readonly TextBlock _labUsed = new() { FontSize = 16 };
    private readonly TextBlock _labGame = new() { FontSize = 16 };
    private readonly TextBlock _labUsedTitle = new();
    private readonly TextBlock _labGameTitle = new();
    private readonly MyHint _hintTooHigh = new() { Theme = MyHintTheme.Yellow, HasBorder = true };

    // 可见性下拉项序号 → LauncherVisibility 枚举（对齐 WPF：废弃值 1 不显示）
    private static readonly LauncherVisibility[] VisibilityMap =
    [
        LauncherVisibility.ExitImmediately,
        LauncherVisibility.HideAndExit,
        LauncherVisibility.HideAndReopen,
        LauncherVisibility.MinimizeAndReopen,
        LauncherVisibility.DoNothing,
    ];

    public PageSetupLaunch()
    {
        var scroll = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Content = new StackPanel { Margin = new Thickness(25, 25, 25, 10) },
        };
        var panMain = (StackPanel)scroll.Content!;
        Children.Add(scroll);

        // ===== 卡 1：启动选项（可见性；标签左 + 控件右，对齐 WPF 表单行） =====
        var cardOptions = new MyCard { Margin = new Thickness(0, 0, 0, 15), Title = Lang.Text("Setup.Launch.Options.Title") };
        var rowOptions = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*"),
            Margin = new Thickness(25, 4, 25, 15),
        };
        var labVisibility = Label(Lang.Text("Setup.Launch.Options.Visibility.Label"));
        labVisibility.Margin = new Thickness(0, 0, 25, 0);
        Grid.SetColumn(labVisibility, 0);
        _comboVisibility.HorizontalAlignment = HorizontalAlignment.Stretch;
        Grid.SetColumn(_comboVisibility, 1);
        rowOptions.Children.Add(labVisibility);
        rowOptions.Children.Add(_comboVisibility);
        cardOptions.Content = rowOptions;
        panMain.Children.Add(cardOptions);

        _comboVisibility.Items = VisibilityMap.Select(v => v switch
        {
            LauncherVisibility.ExitImmediately => Lang.Text("Setup.Launch.Options.Visibility.CloseImmediately"),
            LauncherVisibility.HideAndExit => Lang.Text("Setup.Launch.Options.Visibility.HideAndCloseOnExit"),
            LauncherVisibility.HideAndReopen => Lang.Text("Setup.Launch.Options.Visibility.HideAndReopenOnExit"),
            LauncherVisibility.MinimizeAndReopen => Lang.Text("Setup.Launch.Options.Visibility.Minimize"),
            _ => Lang.Text("Setup.Launch.Options.Visibility.KeepUnchanged"),
        }).ToArray();
        _comboVisibility.SelectionChanged += (_, index, user) =>
        {
            if (user && index >= 0 && index < VisibilityMap.Length)
                Config.Launch.LauncherVisibility = VisibilityMap[index];
        };
        _comboVisibility.SetSelected(
            Math.Max(0, Array.IndexOf(VisibilityMap, Config.Launch.LauncherVisibility)), false);

        // ===== 卡 2：游戏内存 =====
        var cardRam = new MyCard { Margin = new Thickness(0, 0, 0, 15), Title = Lang.Text("Setup.Launch.Memory.Title") };
        var panRam = new StackPanel { Margin = new Thickness(25, 4, 25, 15) };

        // 单选行（纵向，圆点单选对齐 WPF MyRadioBox）
        var panRadio = new StackPanel { Margin = new Thickness(0, 0, 0, 9) };
        _radioAuto.Text = Lang.Text("Setup.Launch.Memory.Auto");
        ToolTip.SetTip(_radioAuto, Lang.Text("Setup.Launch.Memory.Auto.ToolTip"));
        _radioAuto.HorizontalAlignment = HorizontalAlignment.Left;
        _radioCustom.Text = Lang.Text("Common.Option.Customize");
        _radioCustom.HorizontalAlignment = HorizontalAlignment.Left;
        _radioCustom.Margin = new Thickness(0, 4, 0, 6);
        panRadio.Children.Add(_radioAuto);
        panRadio.Children.Add(_radioCustom);
        panRam.Children.Add(panRadio);

        // 最大内存滑块行
        panRam.Children.Add(SliderRow(
            Lang.Text("Setup.Launch.Memory.Max"),
            Lang.Text("Setup.Launch.Memory.Max.ToolTip"),
            _sliderMax, _labMaxValue));
        // 初始内存滑块行
        panRam.Children.Add(SliderRow(
            Lang.Text("Setup.Launch.Memory.Initial"),
            Lang.Text("Setup.Launch.Memory.Initial.ToolTip"),
            _sliderInitial, _labInitialValue));

        // 内存可视化（条上方小灰字标签、细条 6px、条下方数值：已用深色 / 游戏分配主题色，对齐 WPF）
        var panBar = new Grid
        {
            Margin = new Thickness(0, 11, 2, 0),
            ColumnDefinitions = [_colUsed, _colGame, _colEmpty],
            RowDefinitions = new RowDefinitions("Auto,10,Auto,Auto"),
        };
        _labUsedTitle.Text = Lang.Text("Setup.Launch.Memory.UsedTotal");
        _labUsedTitle.Opacity = 0.7;
        _labUsedTitle.FontSize = 11;
        Grid.SetRow(_labUsedTitle, 0);
        Grid.SetColumnSpan(_labUsedTitle, 3);
        panBar.Children.Add(_labUsedTitle);
        _labGameTitle.Text = Lang.Text("Setup.Launch.Memory.GameAllocation");
        _labGameTitle.Opacity = 0.7;
        _labGameTitle.FontSize = 11;
        _labGameTitle.HorizontalAlignment = HorizontalAlignment.Right;
        Grid.SetRow(_labGameTitle, 0);
        Grid.SetColumnSpan(_labGameTitle, 3);
        panBar.Children.Add(_labGameTitle);

        _rectUsed.Height = 6;
        _rectUsed.SetDynamic(Rectangle.FillProperty, "ColorBrushGray4");
        _rectGame.Height = 6;
        _rectGame.SetDynamic(Rectangle.FillProperty, "ColorBrush3");
        _rectUsed.RadiusX = 3;
        _rectGame.RadiusX = 3;
        Grid.SetRow(_rectUsed, 1);
        Grid.SetRow(_rectGame, 1);
        Grid.SetColumn(_rectGame, 1);
        panBar.Children.Add(_rectUsed);
        panBar.Children.Add(_rectGame);

        _labUsed.FontWeight = FontWeight.Bold;
        _labUsed.SetDynamic(TextBlock.ForegroundProperty, "ColorBrushMemory");
        Grid.SetRow(_labUsed, 2);
        Grid.SetColumnSpan(_labUsed, 2);
        panBar.Children.Add(_labUsed);
        _labGame.FontWeight = FontWeight.Bold;
        _labGame.SetDynamic(TextBlock.ForegroundProperty, "ColorBrush1");
        _labGame.HorizontalAlignment = HorizontalAlignment.Right;
        Grid.SetRow(_labGame, 2);
        Grid.SetColumnSpan(_labGame, 3);
        panBar.Children.Add(_labGame);
        panRam.Children.Add(panBar);

        // 内存过多警告
        _hintTooHigh.Text = Lang.Text("Setup.Launch.Memory.Warning.TooMuch");
        _hintTooHigh.Margin = new Thickness(0, 9, 0, 0);
        _hintTooHigh.IsVisible = false;
        panRam.Children.Add(_hintTooHigh);

        cardRam.Content = panRam;
        panMain.Children.Add(cardRam);

        // ===== 卡 3：启动器行为 =====
        var cardBehavior = new MyCard { Margin = new Thickness(0, 0, 0, 15), Title = "启动器行为" };
        var panBehavior = new StackPanel { Margin = new Thickness(25, 4, 25, 15) };
        panBehavior.Children.Add(CheckRow(
            "启动游戏时显示「你知道吗」",
            Config.Preference.ShowLaunchingHint,
            v => Config.Preference.ShowLaunchingHint = v));
        panBehavior.Children.Add(CheckRow(
            "锁定启动器窗口大小",
            Config.Preference.LockWindowSize,
            v => Config.Preference.LockWindowSize = v));
        cardBehavior.Content = panBehavior;
        panMain.Children.Add(cardBehavior);

        // ===== 数据绑定 =====
        _radioAuto.Check += (_, byMouse) =>
        {
            if (!byMouse) return;
            Config.Launch.MemoryAllocationMode = 0;
            _RefreshRamControls();
        };
        _radioCustom.Check += (_, byMouse) =>
        {
            if (!byMouse) return;
            Config.Launch.MemoryAllocationMode = 1;
            _RefreshRamControls();
        };
        _sliderMax.Change += (slider, _) =>
        {
            Config.Launch.CustomMemorySize = slider.Value;
            _labMaxValue.Text = _RamText(AvaloniaMemorySettings.GetRamFromTick(slider.Value));
            _RefreshRamBar();
        };
        _sliderInitial.Change += (slider, _) =>
        {
            Config.Launch.CustomInitialMemorySize = slider.Value;
            _labInitialValue.Text = _InitialText(slider.Value);
        };
        _sliderMax.HintText = v => $"-Xmx {_RamText(AvaloniaMemorySettings.GetRamFromTick(v))}";
        _sliderInitial.HintText = v => v <= 0 ? "不主动设置" : $"-Xms {_RamText(AvaloniaMemorySettings.GetRamFromTick(v))}";

        _RefreshRamControls();
    }

    private static string _RamText(double gb) => $"{gb:0.0} GB";

    private static string _InitialText(int tick) => tick <= 0 ? "未设置" : _RamText(AvaloniaMemorySettings.GetRamFromTick(tick));

    private static TextBlock Label(string text)
    {
        var label = new TextBlock
        {
            Text = text,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Left,
            FontSize = 13,
        };
        label.SetDynamic(TextBlock.ForegroundProperty, "ColorBrushMemory");
        return label;
    }

    private static Control SliderRow(string title, string toolTip, MySlider slider, TextBlock value)
    {
        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"),
            Margin = new Thickness(0, 0, 0, 9),
        };
        var label = Label(title);
        label.Margin = new Thickness(0, 0, 12, 0);
        Grid.SetColumn(label, 0);
        slider.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(slider, 1);
        value.FontSize = 12;
        value.MinWidth = 70;
        value.TextAlignment = TextAlignment.Right;
        value.VerticalAlignment = VerticalAlignment.Center;
        value.HorizontalAlignment = HorizontalAlignment.Right;
        value.SetDynamic(TextBlock.ForegroundProperty, "ColorBrushMemory");
        Grid.SetColumn(value, 2);
        ToolTip.SetTip(label, toolTip);
        grid.Children.Add(label);
        grid.Children.Add(slider);
        grid.Children.Add(value);
        return grid;
    }

    private static Control CheckRow(string text, bool initial, Action<bool> apply)
    {
        var check = new MyCheckBox
        {
            Text = text,
            Checked = initial,
            Height = 28,
            FontSize = 13,
        };
        check.Change += (_, user) =>
        {
            if (user) apply(check.Checked == true);
        };
        return check;
    }

    /// <summary>根据当前配置刷新单选、滑块与内存条。</summary>
    private void _RefreshRamControls()
    {
        var isCustom = Config.Launch.MemoryAllocationMode == 1;
        // 程序恢复选中态（不触发事件，避免回写）
        if (isCustom) _radioCustom.SetChecked(true, false);
        else _radioAuto.SetChecked(true, false);
        _sliderMax.IsEnabled = isCustom;
        _sliderInitial.IsEnabled = isCustom && !Config.Launch.LockMemory;
        _sliderMax.Value = Math.Clamp(Config.Launch.CustomMemorySize, 0, 49);
        _sliderInitial.Value = Math.Clamp(Config.Launch.CustomInitialMemorySize, 0, 49);
        _labMaxValue.Text = _RamText(AvaloniaMemorySettings.GetRamFromTick(_sliderMax.Value));
        _labInitialValue.Text = _InitialText(_sliderInitial.Value);
        _RefreshRamBar();
    }

    /// <summary>刷新内存可视化条（已用/游戏分配/空闲 三段比例与文本）。</summary>
    private void _RefreshRamBar()
    {
        var phyRam = KernelInterop.GetPhysicalMemoryBytes();
        var total = Math.Round(phyRam.Total / 1024.0 / 1024 / 1024, 1);
        var available = Math.Round(phyRam.Available / 1024.0 / 1024 / 1024, 1);
        var used = Math.Max(0, total - available);
        var game = Math.Round(MemorySettings.GetRam(InstanceList.McMcInstanceSelected), 5);

        _labUsed.Text = $"{used:0.0} / {total:0.0} GiB";
        _labGame.Text = $"{game:0.0} GiB";
        _colUsed.Width = new GridLength(Math.Max(0.1, used), GridUnitType.Star);
        _colGame.Width = new GridLength(Math.Max(0.1, game), GridUnitType.Star);
        _colEmpty.Width = new GridLength(Math.Max(0.1, total - used - game), GridUnitType.Star);
        _hintTooHigh.IsVisible = game > total * 0.8;
    }
}
