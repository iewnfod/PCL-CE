using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using PathShape = Avalonia.Controls.Shapes.Path;
using SvgIconControl = PCL.Avalonia.Controls.SvgIcon;

namespace PCL.Avalonia.Controls;

public enum MyIconButtonTheme
{
    /// <summary>主题色：常态 ColorBrush4，悬停 ColorBrush2（默认）。</summary>
    Color,

    /// <summary>白色：常态 #EAF2FE，悬停附加 20% 白色圆形底。</summary>
    White,

    /// <summary>黑色：常态 63% 黑（暗色主题为白），悬停 90%。</summary>
    Black,

    /// <summary>红色：常态 63% #FF4C4C，悬停不透明。</summary>
    Red,

    /// <summary>自定义：以 Foreground 为基色，常态 63% 透明度，悬停不透明。</summary>
    Custom
}

/// <summary>
///     PCL 图形按钮（1:1 复刻 WPF MyIconButton）：圆形背景、矢量图标。<br/>
///     图标：Logo（Path Data）或 SvgIcon（"lucide/x" 名）二选一，后者优先。<br/>
///     交互：按下 PanBack 缩至 0.8；松开回弹（OutBack）；颜色过渡 120/150ms。<br/>
///     宽度未设置时自动取高度（WPF MeasureOverride 同款行为）。
/// </summary>
public class MyIconButton : TemplatedControl
{
    public static readonly StyledProperty<string?> LogoProperty =
        AvaloniaProperty.Register<MyIconButton, string?>(nameof(Logo));

    public static readonly StyledProperty<string?> SvgIconProperty =
        AvaloniaProperty.Register<MyIconButton, string?>(nameof(SvgIcon));

    public static readonly StyledProperty<double> LogoScaleProperty =
        AvaloniaProperty.Register<MyIconButton, double>(nameof(LogoScale), 1d);

    public static readonly new StyledProperty<MyIconButtonTheme> ThemeProperty =
        AvaloniaProperty.Register<MyIconButton, MyIconButtonTheme>(nameof(Theme), MyIconButtonTheme.Color);

    public static readonly StyledProperty<System.Windows.Input.ICommand?> CommandProperty =
        AvaloniaProperty.Register<MyIconButton, System.Windows.Input.ICommand?>(nameof(Command));

    public static readonly StyledProperty<object?> CommandParameterProperty =
        AvaloniaProperty.Register<MyIconButton, object?>(nameof(CommandParameter));

    public static readonly RoutedEvent<RoutedEventArgs> ClickEvent =
        RoutedEvent.Register<MyIconButton, RoutedEventArgs>(nameof(Click), RoutingStrategies.Bubble);

    private Panel? _iconHost;

    /// <summary>矢量图标（WPF Path Data 格式，如 "M0,6L1.5,4.5 …z"）。</summary>
    public string? Logo
    {
        get => GetValue(LogoProperty);
        set => SetValue(LogoProperty, value);
    }

    /// <summary>SVG 图标名（如 "lucide/x"）；设置后优先于 Logo。</summary>
    public string? SvgIcon
    {
        get => GetValue(SvgIconProperty);
        set => SetValue(SvgIconProperty, value);
    }

    /// <summary>图标缩放（SvgIcon 恒为 1，对应 WPF 行为）。</summary>
    public double LogoScale
    {
        get => GetValue(LogoScaleProperty);
        set => SetValue(LogoScaleProperty, value);
    }

    public new MyIconButtonTheme Theme
    {
        get => GetValue(ThemeProperty);
        set => SetValue(ThemeProperty, value);
    }

    public System.Windows.Input.ICommand? Command
    {
        get => GetValue(CommandProperty);
        set => SetValue(CommandProperty, value);
    }

    public object? CommandParameter
    {
        get => GetValue(CommandParameterProperty);
        set => SetValue(CommandParameterProperty, value);
    }

    public event EventHandler<RoutedEventArgs>? Click
    {
        add => AddHandler(ClickEvent, value);
        remove => RemoveHandler(ClickEvent, value);
    }

    static MyIconButton()
    {
        LogoProperty.Changed.AddClassHandler<MyIconButton>((c, _) => c._RebuildIcon());
        SvgIconProperty.Changed.AddClassHandler<MyIconButton>((c, _) => c._RebuildIcon());
    }

    /// <summary>宽度未设置时以高度为边长（WPF 版 MeasureOverride 同款）。</summary>
    protected override Size MeasureOverride(Size availableSize)
    {
        var measured = base.MeasureOverride(availableSize);
        if (double.IsNaN(Width) && !double.IsNaN(Height) && Height > 0d && !double.IsInfinity(Height))
            return new Size(Height, Height);
        return measured;
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _iconHost = e.NameScope.Find<Panel>("PART_IconHost");
        _RebuildIcon();
    }

    private void _RebuildIcon()
    {
        if (_iconHost is null) return;
        _iconHost.Children.Clear();

        Control? icon;
        var scale = 1d;
        if (!string.IsNullOrWhiteSpace(SvgIcon))
            icon = new SvgIcon { Icon = SvgIcon };
        else if (!string.IsNullOrWhiteSpace(Logo))
        {
            try
            {
                icon = new PathShape
                {
                    Data = PathGeometry.Parse(Logo),
                    Stretch = Stretch.Uniform
                };
                scale = LogoScale;
            }
            catch
            {
                return; // 无法解析的几何：不渲染（WPF 版抛异常，此处降级）
            }
        }
        else
            return;

        icon.IsHitTestVisible = false;
        icon.HorizontalAlignment = HorizontalAlignment.Stretch;
        icon.VerticalAlignment = VerticalAlignment.Stretch;
        if (icon is PathShape path)
        {
            path.Bind(PathShape.FillProperty, this.GetObservable(ForegroundProperty));
            icon.RenderTransform = new ScaleTransform(scale, scale);
            icon.RenderTransformOrigin = new RelativePoint(0.5, 0.5, RelativeUnit.Relative);
        }
        else if (icon is SvgIconControl svg)
            svg.Bind(SvgIconControl.IconBrushProperty, this.GetObservable(ForegroundProperty));
        _iconHost.Children.Add(icon);
    }

    private bool _pressed;

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        _pressed = true;
        PseudoClasses.Set(":pressed", true);
        Focus();
        e.Handled = true;
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (!_pressed) return;
        _pressed = false;
        PseudoClasses.Set(":pressed", false);
        if (Bounds.Contains(e.GetPosition(this)))
        {
            RaiseEvent(new RoutedEventArgs(ClickEvent));
            if (Command?.CanExecute(CommandParameter) == true)
                Command.Execute(CommandParameter);
        }
        e.Handled = true;
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        _pressed = false;
        PseudoClasses.Set(":pressed", false);
    }
}
