using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace PCL.Avalonia.Controls;

/// <summary>
///     PCL 按钮（1:1 复刻 WPF MyButton）：描边风格，双层结构。<br/>
///     外层 SemiTransparent 底；内层圆角 3、1px 主题色描边、HalfWhite 底、13px 文字（颜色随描边）。<br/>
///     悬停：描边 → ColorBrush3、底 → ColorBrush7；按下：缩放 0.955；禁用：描边 Gray4。
/// </summary>
[TemplatePart("PART_Fore", typeof(Border))]
public class MyButton : TemplatedControl
{
    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<MyButton, string?>(nameof(Text));

    public static readonly StyledProperty<MyButtonColorType> ColorTypeProperty =
        AvaloniaProperty.Register<MyButton, MyButtonColorType>(nameof(ColorType));

    public static readonly RoutedEvent<RoutedEventArgs> ClickEvent =
        RoutedEvent.Register<MyButton, RoutedEventArgs>(nameof(Click), RoutingStrategies.Bubble);

    private Border? _fore;

    public string? Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    /// <summary>Normal：描边 ColorBrush1；Highlight：ColorBrush2；Red：红系。</summary>
    public MyButtonColorType ColorType
    {
        get => GetValue(ColorTypeProperty);
        set => SetValue(ColorTypeProperty, value);
    }

    /// <summary>WPF 版同名的 PadShape 语义：控件的强制高度（WPF 按钮常设 28/30）。</summary>
    public double ShapeSize
    {
        get => GetValue(ShapeSizeProperty);
        set => SetValue(ShapeSizeProperty, value);
    }

    public static readonly StyledProperty<double> ShapeSizeProperty =
        AvaloniaProperty.Register<MyButton, double>(nameof(ShapeSize), double.NaN);

    public static readonly StyledProperty<System.Windows.Input.ICommand?> CommandProperty =
        AvaloniaProperty.Register<MyButton, System.Windows.Input.ICommand?>(nameof(Command));

    public static readonly StyledProperty<object?> CommandParameterProperty =
        AvaloniaProperty.Register<MyButton, object?>(nameof(CommandParameter));

    /// <summary>命令绑定（与 WPF 版一致）。</summary>
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

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _fore = e.NameScope.Find<Border>("PART_Fore");
    }

    private bool _pressed;

    // 与 MyListItem 一致的事件订阅式点击（Wayland 下 override OnPointerPressed/Released
    // 的 Handled/路由行为不稳定，且 e.Handled=true 会破坏指针捕获）
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        var left = e.GetCurrentPoint(this).Properties.IsLeftButtonPressed;
        ClickTrace.Log($"MyButton[{Text}] Pressed 左键={left} 位置={e.GetPosition(this)} 边界={Bounds}");
        if (!left) return;
        _pressed = true;
        PseudoClasses.Set(":pressed", true);
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        ClickTrace.Log($"MyButton[{Text}] Released _pressed={_pressed} 位置={e.GetPosition(this)} 边界内={Bounds.Contains(e.GetPosition(this))}");
        if (!_pressed) return;
        _pressed = false;
        PseudoClasses.Set(":pressed", false);
        if (!new Rect(0, 0, Bounds.Width, Bounds.Height).Contains(e.GetPosition(this))) return;
        ClickTrace.Log($"MyButton[{Text}] → Click 触发");
        RaiseEvent(new RoutedEventArgs(ClickEvent));
        if (Command?.CanExecute(CommandParameter) == true)
            Command.Execute(CommandParameter);
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        if (_pressed) ClickTrace.Log($"MyButton[{Text}] Exited（按下状态被清除）");
        _pressed = false;
        PseudoClasses.Set(":pressed", false);
    }
}

public enum MyButtonColorType
{
    Normal,
    Highlight,
    Red
}
