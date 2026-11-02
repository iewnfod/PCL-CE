using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Controls.Shapes;
using Avalonia.Media;
using Avalonia.Threading;

namespace PCL.Avalonia.Controls;

/// <summary>
///     加载指示器（对应 WPF 版 MyLoading）：主题色圆环旋转。
/// </summary>
public class MyLoading : ContentControl
{
    private readonly RotateTransform _rotate = new();
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(16) };

    public static readonly StyledProperty<double> SizeProperty =
        AvaloniaProperty.Register<MyLoading, double>(nameof(Size), 22);

    /// <summary>指示器直径（像素）。</summary>
    public double Size
    {
        get => GetValue(SizeProperty);
        set => SetValue(SizeProperty, value);
    }

    public MyLoading()
    {
        HorizontalAlignment = HorizontalAlignment.Center;
        VerticalAlignment = VerticalAlignment.Center;

        var app = Application.Current;
        IBrush? accent = null;
        if (app?.Resources.TryGetResource("ColorBrush1", app.ActualThemeVariant, out var color) == true &&
            color is IBrush brush)
            accent = brush;
        accent ??= Brushes.DodgerBlue;

        var ringSize = Size;
        var ring = new Ellipse
        {
            Width = ringSize,
            Height = ringSize,
            StrokeThickness = 3,
            StrokeDashArray = [ringSize * 0.66, ringSize * 0.5],
            Stroke = accent,
            RenderTransform = _rotate,
        };
        var track = new Ellipse
        {
            Width = ringSize,
            Height = ringSize,
            StrokeThickness = 3,
            Stroke = accent,
            Opacity = 0.15,
        };

        Content = new Panel
        {
            Width = ringSize,
            Height = ringSize,
            Children = { track, ring }
        };

        _timer.Tick += (_, _) => _rotate.Angle = (_rotate.Angle + 6) % 360;
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _timer.Start();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _timer.Stop();
    }
}
