using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Threading;
using PCL.Core.UI.Theme;

namespace PCL.Avalonia.Controls;

public enum MyHintTheme
{
    Blue = 0,
    Red = 1,
    Yellow = 2
}

/// <summary>
///     PCL 提示条（1:1 复刻 WPF MyHint）：左侧 3px 主题色条（圆角 2）、HSL 主题配色。<br/>
///     背景 = (hue, 90, L7)、边框与文字 = (hue, 90, L2)；Blue 210° / Red 355° / Yellow 40°。<br/>
///     CanClose 显示右上关闭钮（写入 RelativeSetup 配置后不再显示）；明暗切换自动重算。
/// </summary>
public class MyHint : Border
{
    private readonly TextBlock _labText;
    private readonly MyIconButton _btnClose;

    /// <summary>配色主题（默认 Red）。</summary>
    public new MyHintTheme Theme { get; set; } = MyHintTheme.Red;

    /// <summary>提示文本。</summary>
    public string? Text
    {
        get => _labText.Text;
        set => _labText.Text = value;
    }

    /// <summary>是否显示右上角关闭按钮（关闭后写入 RelativeSetup 配置并不再显示）。</summary>
    public bool CanClose
    {
        get => _btnClose.IsVisible;
        set => _btnClose.IsVisible = value;
    }

    /// <summary>是否显示完整边框（false 仅左侧色条；true 为 3,1,1,1 边框）。</summary>
    public bool HasBorder
    {
        get => BorderThickness.Top > 0d;
        set => BorderThickness = value
            ? new Thickness(3d, 1d, 1d, 1d)
            : new Thickness(3d, 0d, 0d, 0d);
    }

    /// <summary>关闭按钮写入的配置键（CanClose 时生效）。</summary>
    public string RelativeSetup { get; set; } = "";

    public MyHint()
    {
        CornerRadius = new CornerRadius(2);
        BorderThickness = new Thickness(3, 0, 0, 0);
        HorizontalAlignment = HorizontalAlignment.Stretch;

        _labText = new TextBlock
        {
            LineHeight = 16,
            Margin = new Thickness(0, 0, 0, -1),
            Padding = new Thickness(12, 9),
            VerticalAlignment = VerticalAlignment.Center,
            TextWrapping = TextWrapping.Wrap,
            IsHitTestVisible = false
        };
        _btnClose = new MyIconButton
        {
            Theme = MyIconButtonTheme.Custom,
            Margin = new Thickness(0, 0, 8, 0),
            Height = 20,
            Width = 20,
            VerticalAlignment = VerticalAlignment.Center,
            SvgIcon = "lucide/x",
            IsVisible = false
        };
        _btnClose.Click += (_, _) => _OnClose();

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        Grid.SetColumn(_labText, 0);
        Grid.SetColumn(_btnClose, 1);
        grid.Children.Add(_labText);
        grid.Children.Add(_btnClose);
        Child = grid;

        Transitions = new Transitions
        {
            new DoubleTransition { Property = OpacityProperty, Duration = TimeSpan.FromMilliseconds(200) },
            new DoubleTransition { Property = HeightProperty, Duration = TimeSpan.FromMilliseconds(150) }
        };

        AttachedToVisualTree += (_, _) =>
        {
            ThemeService.ColorModeChanged += _OnThemeChanged;
            _UpdateUI();
            // CanClose 且配置中已存在：不再显示（WPF MyHint_Loaded 同款）
            if (CanClose && global::PCL.Core.App.Configuration.ConfigService.TryGetConfigItemNoType(
                    RelativeSetup, out var item) && item.GetValueNoType() is not null)
                IsVisible = false;
        };
        DetachedFromVisualTree += (_, _) => ThemeService.ColorModeChanged -= _OnThemeChanged;
    }

    private void _OnThemeChanged(bool isDarkMode, global::PCL.Core.App.ColorTheme theme) => _UpdateUI();

    private void _UpdateUI()
    {
        var hue = Theme switch
        {
            MyHintTheme.Blue => 210d,
            MyHintTheme.Red => 355d,
            MyHintTheme.Yellow => 40d,
            _ => 355d
        };

        var tone = ThemeService.CurrentTone;
        Background = new ImmutableSolidColorBrush(ColorHelper.FromHsl2(hue, 90d, tone.L7 * 100d));
        var dark = new ImmutableSolidColorBrush(ColorHelper.FromHsl2(hue, 90d, tone.L2 * 100d));
        BorderBrush = dark;
        _labText.Foreground = dark;
        _btnClose.Foreground = dark;

        // 通过 HasBorder 副作用做范围检查（WPF 同款）
        HasBorder = HasBorder;
    }

    private void _OnClose()
    {
        if (global::PCL.Core.App.Configuration.ConfigService.TryGetConfigItemNoType(RelativeSetup, out var item))
            item.SetValueNoType(true);

        // 关闭动画：0.92 缩放 + 淡出 + 高度收起（WPF AniDispose）
        IsHitTestVisible = false;
        Height = Bounds.Height;
        Opacity = 0;
        RenderTransform = new ScaleTransform(0.92, 0.92);
        RenderTransformOrigin = new RelativePoint(0.5, 0.5, RelativeUnit.Relative);
        DispatcherTimer.RunOnce(() => IsVisible = false, TimeSpan.FromMilliseconds(250));
    }
}

/// <summary>WPF Common.MyColor 的 HSL 算法 1:1 搬运（FromHsl2 感知补偿 + FromHsl + Hue）。</summary>
public static class ColorHelper
{
    public static Color FromHsl2(double h, double s, double l)
    {
        if (s == 0d)
            return Color.FromRgb(_ToByte(l * 2.55d), _ToByte(l * 2.55d), _ToByte(l * 2.55d));

        h = (h + 3600000d) % 360d;
        double[] cent =
        [
            +0.1d, -0.06d, -0.3d, -0.19d, -0.15d, -0.24d, -0.32d,
            -0.09d, +0.18d, +0.05d, -0.12d, -0.02d, +0.1d, -0.06d
        ];
        var center = h / 30.0d;
        var intCenter = (int)Math.Floor(center);
        center = 50d
                 - ((1d - center + intCenter) * cent[intCenter]
                    + (center - intCenter) * cent[intCenter + 1])
                 * s;
        l = (l < center
                ? l / center
                : 1d + (l - center) / (100d - center))
            * 50d;
        return FromHsl(h, s, l);
    }

    private static Color FromHsl(double h, double s, double l)
    {
        if (s == 0d)
        {
            var gray = _ToByte(l * 2.55d);
            return Color.FromRgb(gray, gray, gray);
        }

        var hn = h / 360d;
        var sn = s / 100d;
        var ln = l / 100d;
        sn = ln < 0.5d ? sn * ln + ln : sn * (1.0d - ln) + ln;
        ln = 2d * ln - sn;
        return Color.FromRgb(
            _ToByte(255d * Hue(ln, sn, hn + 1d / 3d)),
            _ToByte(255d * Hue(ln, sn, hn)),
            _ToByte(255d * Hue(ln, sn, hn - 1d / 3d)));
    }

    private static double Hue(double v1, double v2, double vh)
    {
        if (vh < 0d) vh += 1d;
        if (vh > 1d) vh -= 1d;
        return vh switch
        {
            < 0.16667d => v1 + (v2 - v1) * 6d * vh,
            < 0.5d => v2,
            < 0.66667d => v1 + (v2 - v1) * (4d - vh * 6d),
            _ => v1
        };
    }

    private static byte _ToByte(double value) =>
        (byte)Math.Clamp(Math.Round(value), 0, 255);
}
