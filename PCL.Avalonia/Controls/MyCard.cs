using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;

namespace PCL.Avalonia.Controls;

/// <summary>
///     PCL 卡片（1:1 复刻 WPF MyCard）：半径 5、半透明底、主题色阴影（悬停 0.07→0.4）。<br/>
///     标题：左上 Margin(15,12)、13px Bold、ColorBrush1（悬停 → ColorBrush2）。<br/>
///     折叠：右上 V 形箭头（旋转动画）、折叠高度 40、高度动画 150ms 缓出。
/// </summary>
[TemplatePart("PART_Shadow", typeof(Border))]
[TemplatePart("PART_Header", typeof(Border))]
public class MyCard : ContentControl
{
    public const double SwapedHeight = 40;

    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<MyCard, string?>(nameof(Title), "");

    public static readonly StyledProperty<bool> CanSwapProperty =
        AvaloniaProperty.Register<MyCard, bool>(nameof(CanSwap));

    public static readonly StyledProperty<bool> IsSwappedProperty =
        AvaloniaProperty.Register<MyCard, bool>(nameof(IsSwapped));

    public static readonly RoutedEvent<RoutedEventArgs> SwapEvent =
        RoutedEvent.Register<MyCard, RoutedEventArgs>(nameof(Swap), RoutingStrategies.Bubble);

    public string? Title
    {
        get => GetValue(TitleProperty);
        // null 归一为空串：无标题卡由模板样式收起标题行
        set => SetValue(TitleProperty, string.IsNullOrEmpty(value) ? "" : value);
    }

    /// <summary>是否允许点击标题区折叠。</summary>
    public bool CanSwap
    {
        get => GetValue(CanSwapProperty);
        set => SetValue(CanSwapProperty, value);
    }

    /// <summary>是否已折叠（WPF 同名属性）。</summary>
    public bool IsSwapped
    {
        get => GetValue(IsSwappedProperty);
        set => SetValue(IsSwappedProperty, value);
    }

    public event EventHandler<RoutedEventArgs>? Swap
    {
        add => AddHandler(SwapEvent, value);
        remove => RemoveHandler(SwapEvent, value);
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (e.NameScope.Find<Border>("PART_Header") is { } header)
        {
            // 无标题卡：标题行收起（local value，确保覆盖模板默认 40px）
            if (string.IsNullOrEmpty(Title)) header.Height = 0;
            header.PointerPressed += HeaderOnPointerPressed;
            header.PointerReleased += HeaderOnPointerReleased;
            header.Cursor = new Cursor(StandardCursorType.Hand);
        }
        // 主题色投影：ColorObject1（WPF MyDropShadow 行为）
        if (e.NameScope.Find<Border>("PART_Shadow") is { } shadow)
        {
            var app = Application.Current;
            if (app?.Resources.TryGetResource("ColorObject1", app.ActualThemeVariant, out var color) == true &&
                color is Color accent)
                shadow.BoxShadow = BoxShadows.Parse($"0 0 6 2 {accent}");
        }
    }

    private bool _headerPressed;

    private void HeaderOnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        _headerPressed = true;
        e.Handled = true;
    }

    private void HeaderOnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (!_headerPressed) return;
        _headerPressed = false;
        if (!CanSwap) return;
        IsSwapped = !IsSwapped;
        RaiseEvent(new RoutedEventArgs(SwapEvent));
        e.Handled = true;
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsSwappedProperty)
            PseudoClasses.Set(":swapped", IsSwapped);
    }
}
