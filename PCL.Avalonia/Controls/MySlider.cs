using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;

namespace PCL.Avalonia.Controls;

/// <summary>
///     PCL 滑块（复刻 WPF MySlider）：16px 高轨道、1px 底线（30% Bg0）、2px 前景线、
///     10px 圆点（Bg0 填充 + 描边，悬停放大），拖动时圆点上方显示值气泡。<br/>
///     拖动与键盘（←/→，Ctrl ×10）均实时触发 <see cref="Change" />（user=true）。
/// </summary>
public class MySlider : Grid
{
    private const double DotSize = 10d;
    private const double TrackHeight = 16d;

    private readonly Rectangle _lineBack = new() { Height = 1 };
    private readonly Rectangle _lineFore = new() { Height = 2 };
    private readonly Ellipse _dot = new()
    {
        Width = DotSize,
        Height = DotSize,
        StrokeThickness = 1.2,
        RenderTransformOrigin = new RelativePoint(0.5, 0.5, RelativeUnit.Relative),
        RenderTransform = new ScaleTransform(1, 1),
    };

    private readonly Border _bubble = new()
    {
        IsVisible = false,
        CornerRadius = new CornerRadius(3),
        BorderThickness = new Thickness(1.5),
        Padding = new Thickness(7, 4),
        MaxWidth = 500,
        HorizontalAlignment = HorizontalAlignment.Left,
        VerticalAlignment = VerticalAlignment.Top,
        ZIndex = 10,
        Child = new TextBlock { FontSize = 12, TextWrapping = TextWrapping.Wrap },
    };

    private bool _isDragging;
    private bool _isUpdatingValue;

    public MySlider()
    {
        Focusable = true;
        ClipToBounds = false;
        Background = Brushes.Transparent;

        _lineBack.SetDynamic(Rectangle.FillProperty, "ColorBrushBg0");
        _lineBack.Opacity = 0.3;
        _lineFore.SetDynamic(Rectangle.FillProperty, "ColorBrushBg0");
        _dot.SetDynamic(Ellipse.FillProperty, "ColorBrushBg0");
        _dot.SetDynamic(Ellipse.StrokeProperty, "ColorBrushBg0");
        _bubble.SetDynamic(Border.BackgroundProperty, "ColorBrushWhite");
        _bubble.SetDynamic(Border.BorderBrushProperty, "ColorBrush1");
        _bubble.BoxShadow = new BoxShadows(new BoxShadow { Blur = 4, OffsetY = 2, Color = Color.FromArgb(102, 0, 0, 0) });
        if (_bubble.Child is TextBlock text)
            text.SetDynamic(TextBlock.ForegroundProperty, "ColorBrush1");

        Children.AddRange([_lineBack, _lineFore, _dot, _bubble]);

        PointerPressed += _OnPointerPressed;
        PointerMoved += _OnPointerMoved;
        PointerReleased += _OnPointerReleased;
        PointerEntered += _OnPointerEntered;
        PointerExited += _OnPointerExited;
        KeyDown += _OnKeyDown;
    }

    /// <summary>当前刻度值（0 – MaxValue，超出自动截断）。</summary>
    public int Value
    {
        get => field;
        set
        {
            var clamped = Math.Clamp(value, 0, Math.Max(0, MaxValue));
            if (clamped == Value) return;
            field = clamped;
            _OnValueChanged();
        }
    }

    /// <summary>最大刻度值（默认 100）。</summary>
    public int MaxValue { get; set; } = 100;

    /// <summary>值变化（user 区分用户操作与程序赋值，对齐 WPF MySlider.Change）。</summary>
    public event Action<MySlider, bool>? Change;

    /// <summary>拖动中的气泡文本（null 时显示刻度值）。</summary>
    public Func<int, string>? HintText { get; set; }

    private double _UsableWidth => Math.Max(0, Bounds.Width - DotSize);

    private double _DotOffset => MaxValue <= 0 ? 0 : Math.Min(_UsableWidth, (double)Value / MaxValue * _UsableWidth);

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsInfinity(availableSize.Width) ? 100 : availableSize.Width;
        var height = Math.Max(availableSize.Height is double h && !double.IsInfinity(h) ? h : TrackHeight, TrackHeight);
        _lineBack.Measure(new Size(width, 1));
        _lineFore.Measure(new Size(width, 2));
        _dot.Measure(Size.Infinity);
        _bubble.Measure(new Size(width, double.PositiveInfinity));
        return new Size(Math.Max(width, DotSize), height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var height = finalSize.Height;
        var centerY = height / 2;
        var usable = Math.Max(0, finalSize.Width - DotSize);
        var foreWidth = MaxValue <= 0 ? 0 : Math.Min(usable, (double)Value / MaxValue * usable);
        var dotOffset = foreWidth;

        _lineBack.Arrange(new Rect(0, centerY - 0.5, finalSize.Width, 1));
        _lineFore.Arrange(new Rect(0, centerY - 1, foreWidth + 1, 2));
        _dot.Arrange(new Rect(dotOffset, centerY - DotSize / 2, DotSize, DotSize));
        _bubble.Arrange(new Rect(
            Math.Clamp(dotOffset + DotSize / 2 - _bubble.DesiredSize.Width / 2, 0,
                Math.Max(0, finalSize.Width - _bubble.DesiredSize.Width)),
            0, Math.Max(0, _bubble.DesiredSize.Width), Math.Max(0, _bubble.DesiredSize.Height)));
        return new Size(finalSize.Width, height);
    }

    private void _OnValueChanged()
    {
        InvalidateMeasure();
        InvalidateArrange();
        if (_isUpdatingValue) return;
        Change?.Invoke(this, false);
    }

    private int _ValueFromX(double x)
    {
        var usable = _UsableWidth;
        var ratio = usable <= 0 ? 0 : Math.Clamp((x - DotSize / 2) / usable, 0d, 1d);
        return (int)Math.Round(ratio * MaxValue);
    }

    private void _SetValue(int value, bool user)
    {
        _isUpdatingValue = true;
        try
        {
            Value = value;
        }
        finally
        {
            _isUpdatingValue = false;
        }
        if (user) Change?.Invoke(this, true);
    }

    private void _OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!IsEnabled) return;
        var point = e.GetCurrentPoint(this);
        if (!point.Properties.IsLeftButtonPressed) return;
        _isDragging = true;
        Focus();
        e.Pointer.Capture(this);
        _SetValue(_ValueFromX(point.Position.X), true);
        _ShowBubble(true);
        e.Handled = true;
    }

    private void _OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (!_isDragging) return;
        _SetValue(_ValueFromX(e.GetCurrentPoint(this).Position.X), true);
        _ShowBubble(true);
    }

    private void _OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (!_isDragging) return;
        _isDragging = false;
        e.Pointer.Capture(null);
        _ShowBubble(false);
    }

    private void _OnPointerEntered(object? sender, PointerEventArgs e)
    {
        if (_dot.RenderTransform is ScaleTransform scale)
            scale.ScaleX = scale.ScaleY = 1.15;
    }

    private void _OnPointerExited(object? sender, PointerEventArgs e)
    {
        if (_dot.RenderTransform is ScaleTransform scale)
            scale.ScaleX = scale.ScaleY = 1;
        if (!_isDragging) _ShowBubble(false);
    }

    private void _OnKeyDown(object? sender, KeyEventArgs e)
    {
        var step = e.KeyModifiers.HasFlag(KeyModifiers.Control) ? 10 : 1;
        switch (e.Key)
        {
            case Key.Left:
            case Key.Down:
                _SetValue(Value - step, true);
                e.Handled = true;
                break;
            case Key.Right:
            case Key.Up:
                _SetValue(Value + step, true);
                e.Handled = true;
                break;
        }
    }

    private void _ShowBubble(bool show)
    {
        _bubble.IsVisible = show;
        if (show && _bubble.Child is TextBlock text)
            text.Text = HintText?.Invoke(Value) ?? Value.ToString();
        _bubble.InvalidateMeasure();
        InvalidateArrange();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsEnabledProperty)
            Opacity = IsEnabled ? 1 : 0.5;
    }
}
