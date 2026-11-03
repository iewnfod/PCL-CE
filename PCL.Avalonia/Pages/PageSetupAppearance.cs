using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Input;
using Avalonia.Media;
using PCL.Avalonia.Controls;
using PCL.Core.App;
using PCL.Core.App.Localization;
using PCL.Core.UI.Theme;

namespace PCL.Avalonia.Pages;

/// <summary>
///     设置-个性化页（复刻 WPF PageSetupUI 的主题子集）：
///     配色模式（浅色/深色/跟随系统）+ 浅色/深色主题色（色块选择，选中描边）。
///     写入 Config 后由核心层 ThemeService 自动应用全局配色。
/// </summary>
public class PageSetupAppearance : Grid
{
    private readonly MyComboBox _comboMode = new();
    private readonly Panel _panLightSwatches = new StackPanel { Orientation = Orientation.Horizontal };
    private readonly Panel _panDarkSwatches = new StackPanel { Orientation = Orientation.Horizontal };
    private readonly List<(ColorTheme Theme, Border Block, TextBlock Caption)> _lightSwatches = [];
    private readonly List<(ColorTheme Theme, Border Block, TextBlock Caption)> _darkSwatches = [];

    private static readonly (ColorTheme Theme, string Label)[] LightSwatchItems =
    [
        (ColorTheme.SkyBlue, "Setup.Ui.Theme.Color.SkyBlue"),
        (ColorTheme.CatBlue, "Setup.Ui.Theme.Color.CatBlue"),
        (ColorTheme.DeathBlue, "Setup.Ui.Theme.Color.CrashBlue"),
    ];

    private static readonly (ColorTheme Theme, string Label)[] DarkSwatchItems =
    [
        (ColorTheme.SkyBlue, "Setup.Ui.Theme.Color.SkyBlue"),
        (ColorTheme.CatBlue, "Setup.Ui.Theme.Color.CatBlue"),
        (ColorTheme.DeathBlue, "Setup.Ui.Theme.Color.CrashBlue"),
    ];

    public PageSetupAppearance()
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
            Title = Lang.Text("Setup.Ui.Theme.Title"),
        };
        var pan = new StackPanel { Margin = new Thickness(25, 4, 25, 15) };

        // 配色模式
        var gridMode = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*"),
            Margin = new Thickness(0, 0, 0, 12),
        };
        var labMode = Label(Lang.Text("Setup.Ui.Theme.Title"));
        labMode.Margin = new Thickness(0, 0, 25, 0);
        Grid.SetColumn(labMode, 0);
        _comboMode.Items =
        [
            Lang.Text("Setup.Ui.Theme.Light"),
            Lang.Text("Setup.Ui.Theme.Dark"),
            Lang.Text("Setup.Ui.Theme.FollowSystem"),
        ];
        _comboMode.SetSelected((int)Config.Preference.Theme.ColorMode, false);
        _comboMode.SelectionChanged += (_, index, user) =>
        {
            if (user && index >= 0 && index <= 2)
                Config.Preference.Theme.ColorMode = (ColorMode)index;
        };
        Grid.SetColumn(_comboMode, 1);
        _comboMode.HorizontalAlignment = HorizontalAlignment.Stretch;
        gridMode.Children.Add(labMode);
        gridMode.Children.Add(_comboMode);
        pan.Children.Add(gridMode);

        // 浅色配色
        var labLight = Label(Lang.Text("Setup.Ui.Theme.LightColor"));
        ToolTip.SetTip(labLight, Lang.Text("Setup.Ui.Theme.LightColor.ToolTip"));
        labLight.Margin = new Thickness(0, 0, 0, 8);
        pan.Children.Add(labLight);
        _BuildSwatches(_panLightSwatches, _lightSwatches, LightSwatchItems, false);
        pan.Children.Add(_panLightSwatches);

        // 深色配色
        var labDark = Label(Lang.Text("Setup.Ui.Theme.DarkColor"));
        ToolTip.SetTip(labDark, Lang.Text("Setup.Ui.Theme.DarkColor.ToolTip"));
        labDark.Margin = new Thickness(0, 14, 0, 8);
        pan.Children.Add(labDark);
        _BuildSwatches(_panDarkSwatches, _darkSwatches, DarkSwatchItems, true);
        pan.Children.Add(_panDarkSwatches);

        card.Content = pan;
        panMain.Children.Add(card);
    }

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

    private void _BuildSwatches(Panel host, List<(ColorTheme, Border, TextBlock)> registry,
        (ColorTheme Theme, string Label)[] items, bool forDark)
    {
        registry.Clear();
        if (host is StackPanel panel) panel.Children.Clear();
        foreach (var (theme, labelKey) in items)
        {
            var themeValue = theme;
            var block = new Border
            {
                Width = 34,
                Height = 34,
                CornerRadius = new CornerRadius(7),
                Background = new SolidColorBrush(_SwatchColor(themeValue, forDark)),
                Cursor = new Cursor(StandardCursorType.Hand),
                Tag = themeValue,
                HorizontalAlignment = HorizontalAlignment.Center,
            };
            ToolTip.SetTip(block, Lang.Text(labelKey));
            block.PointerPressed += (_, e) =>
            {
                e.Handled = true;
                if (forDark) Config.Preference.Theme.DarkColor = themeValue;
                else Config.Preference.Theme.LightColor = themeValue;
                _RefreshSelection(registry, forDark
                    ? Config.Preference.Theme.DarkColor
                    : Config.Preference.Theme.LightColor);
            };
            var caption = new TextBlock
            {
                Text = Lang.Text(labelKey),
                FontSize = 11,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 4, 0, 0),
            };
            caption.SetDynamic(TextBlock.ForegroundProperty, "ColorBrushMemory");
            var swatch = new StackPanel
            {
                Margin = new Thickness(0, 0, 16, 0),
                Children =
                {
                    block,
                    caption,
                },
            };
            registry.Add((themeValue, block, caption));
            if (host is StackPanel p) p.Children.Add(swatch);
        }
        _RefreshSelection(registry, forDark
            ? Config.Preference.Theme.DarkColor
            : Config.Preference.Theme.LightColor);
    }

    private static void _RefreshSelection(List<(ColorTheme Theme, Border Block, TextBlock Caption)> registry, ColorTheme selected)
    {
        foreach (var (theme, block, caption) in registry)
        {
            if (theme == selected)
            {
                block.BorderThickness = new Thickness(2.5);
                block.SetDynamic(Border.BorderBrushProperty, "ColorBrushWhite");
                block.Margin = new Thickness(-2.5, -2.5, 0, 0);
                caption.SetDynamic(TextBlock.ForegroundProperty, "ColorBrush1");
                caption.FontWeight = FontWeight.Bold;
            }
            else
            {
                block.BorderThickness = new Thickness(0);
                block.Margin = new Thickness(0);
                caption.SetDynamic(TextBlock.ForegroundProperty, "ColorBrushMemory");
                caption.FontWeight = FontWeight.Normal;
            }
        }
    }

    /// <summary>主题色预览（与核心层 _CalculateColors 的 Color2 同算法：Lch → OkLab → sRGB）。</summary>
    private static Color _SwatchColor(ColorTheme theme, bool dark)
    {
        var (hue, lightAdj, chromaAdj) = theme switch
        {
            ColorTheme.SkyBlue => (235, 0.36, 0.2),
            ColorTheme.CatBlue => (255, 0d, -0.2),
            ColorTheme.DeathBlue => (268, -0.05, -0.1),
            _ => (275, -0.03, -0.35),
        };
        var tone = dark ? ThemeService.ToneProfiles.Dark : ThemeService.ToneProfiles.Light;
        var l = _AdjustLinear(tone.L2, lightAdj);
        var c = _AdjustLinear(tone.C2, chromaAdj);
        var (a, r, g, b) = LabColor.FromLch(l, c, hue).ToScRgb();
        return Color.FromArgb(
            (byte)Math.Clamp(Math.Round(a * 255), 0, 255),
            (byte)Math.Clamp(Math.Round(r * 255), 0, 255),
            (byte)Math.Clamp(Math.Round(g * 255), 0, 255),
            (byte)Math.Clamp(Math.Round(b * 255), 0, 255));
    }

    private static double _AdjustLinear(double value, double adjustment)
    {
        if (adjustment == 0) return value;
        value = Math.Clamp(value, 0.0, 1.0);
        adjustment = Math.Clamp(adjustment, -1.0, 1.0);
        return adjustment switch
        {
            > 0 => value + (1.0 - value) * adjustment,
            _ => value + value * adjustment,
        };
    }
}
