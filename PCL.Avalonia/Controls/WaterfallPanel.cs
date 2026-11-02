using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;

namespace PCL.Avalonia.Controls;

/// <summary>
///     瀑布流布局面板（简化复刻 PCL.Core.Wpf WaterfallPanel）：
///     列宽 = Min(MaxItemWidth, (可用宽 - (列数-1)×ColumnSpacing) / 列数)，每项放入当前最矮列。
/// </summary>
public class WaterfallPanel : Panel
{
    public static readonly StyledProperty<double> ColumnSpacingProperty =
        AvaloniaProperty.Register<WaterfallPanel, double>(nameof(ColumnSpacing), 14d);

    public static readonly StyledProperty<double> RowSpacingProperty =
        AvaloniaProperty.Register<WaterfallPanel, double>(nameof(RowSpacing), 14d);

    public static readonly StyledProperty<double> MaxItemWidthProperty =
        AvaloniaProperty.Register<WaterfallPanel, double>(nameof(MaxItemWidth), 240d);

    public double ColumnSpacing
    {
        get => GetValue(ColumnSpacingProperty);
        set => SetValue(ColumnSpacingProperty, value);
    }

    public double RowSpacing
    {
        get => GetValue(RowSpacingProperty);
        set => SetValue(RowSpacingProperty, value);
    }

    public double MaxItemWidth
    {
        get => GetValue(MaxItemWidthProperty);
        set => SetValue(MaxItemWidthProperty, value);
    }

    static WaterfallPanel()
    {
        AffectsMeasure<WaterfallPanel>(ColumnSpacingProperty, RowSpacingProperty, MaxItemWidthProperty);
    }

    private double[]? _columnHeights;
    private double _lastArrangeWidth;

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsInfinity(availableSize.Width) ? 1280 : availableSize.Width;
        if (width < MaxItemWidth)
        {
            // 单列
            var height = 0d;
            foreach (var child in Children)
            {
                child.Measure(new Size(width, double.PositiveInfinity));
                height += child.DesiredSize.Height + RowSpacing;
            }

            _columnHeights = [height];
            _lastArrangeWidth = width;
            return new Size(width, Math.Max(0, height - RowSpacing));
        }

        var columns = Math.Max(1, (int)Math.Floor(
            (width + ColumnSpacing) / (MaxItemWidth + ColumnSpacing)));
        var itemWidth = (width - (columns - 1) * ColumnSpacing) / columns;
        _columnHeights = new double[columns];

        foreach (var child in Children)
        {
            // 先以列宽测出自然高度，再挑最矮列
            child.Measure(new Size(itemWidth, double.PositiveInfinity));
            var target = 0;
            for (var i = 1; i < columns; i++)
                if (_columnHeights[i] < _columnHeights[target] - 0.01)
                    target = i;
            _columnHeights[target] += child.DesiredSize.Height + RowSpacing;
        }

        var maxHeight = 0d;
        foreach (var h in _columnHeights) maxHeight = Math.Max(maxHeight, h);
        _lastArrangeWidth = width;
        return new Size(width, Math.Max(0, maxHeight - RowSpacing));
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var width = finalSize.Width;
        if (Math.Abs(width - _lastArrangeWidth) > 0.5)
            MeasureOverride(new Size(width, double.PositiveInfinity));

        var columns = _columnHeights?.Length ?? 1;
        if (columns == 0) columns = 1;
        var columnTops = new double[columns];

        if (columns == 1)
        {
            var y = 0d;
            foreach (var child in Children)
            {
                child.Arrange(new Rect(0, y, width, child.DesiredSize.Height));
                y += child.DesiredSize.Height + RowSpacing;
            }

            return new Size(width, Math.Max(0, y - RowSpacing));
        }

        var itemWidth = (width - (columns - 1) * ColumnSpacing) / columns;
        foreach (var child in Children)
        {
            var target = 0;
            for (var i = 1; i < columns; i++)
                if (columnTops[i] < columnTops[target] - 0.01)
                    target = i;
            child.Arrange(new Rect(target * (itemWidth + ColumnSpacing), columnTops[target],
                itemWidth, child.DesiredSize.Height));
            columnTops[target] += child.DesiredSize.Height + RowSpacing;
        }

        var maxHeight = 0d;
        foreach (var h in columnTops) maxHeight = Math.Max(maxHeight, h);
        return new Size(width, Math.Max(0, maxHeight - RowSpacing));
    }
}
