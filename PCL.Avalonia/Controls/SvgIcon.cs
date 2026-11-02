using System.Collections.Concurrent;
using System.Xml.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Platform;

namespace PCL.Avalonia.Controls;

/// <summary>
///     SVG 图标控件（1:1 复刻 WPF PCL.Core.Wpf SvgIcon 的 lucide 渲染效果）。<br/>
///     图标包以 AvaloniaResource 共享自 PCL.Core.Wpf/UI/Assets/IconPacks；<br/>
///     lucide 全系为 stroke 线条图标（stroke-width 2、round cap/join、viewBox 24），
///     此处以几何 + Pen 精确呈现，多 path 合并为单几何。Icon 名格式："lucide/x"。
/// </summary>
public class SvgIcon : Control
{
    private const string DefaultPack = "default";

    private static readonly ConcurrentDictionary<string, Geometry?> Cache = new(StringComparer.OrdinalIgnoreCase);

    public static readonly StyledProperty<string?> IconProperty =
        AvaloniaProperty.Register<SvgIcon, string?>(nameof(Icon));

    public static readonly StyledProperty<IBrush?> IconBrushProperty =
        AvaloniaProperty.Register<SvgIcon, IBrush?>(nameof(IconBrush));

    public static readonly StyledProperty<double> StrokeWidthProperty =
        AvaloniaProperty.Register<SvgIcon, double>(nameof(StrokeWidth), 2d);

    private Geometry? _geometry;

    public string? Icon
    {
        get => GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    public IBrush? IconBrush
    {
        get => GetValue(IconBrushProperty);
        set => SetValue(IconBrushProperty, value);
    }

    /// <summary>线条宽度（lucide 默认 2，viewBox 24 坐标系，随缩放等比）。</summary>
    public double StrokeWidth
    {
        get => GetValue(StrokeWidthProperty);
        set => SetValue(StrokeWidthProperty, value);
    }

    static SvgIcon()
    {
        IconProperty.Changed.AddClassHandler<SvgIcon>((c, _) => c._Reload());
        AffectsRender<SvgIcon>(IconBrushProperty, StrokeWidthProperty);
    }

    private void _Reload() => _geometry = _Load(Icon);

    protected override Size MeasureOverride(Size availableSize)
    {
        // 图标按 24x24 viewBox 等比缩放；无限大约束时按 24 度量
        var side = Math.Min(
            double.IsInfinity(availableSize.Width) ? 24d : availableSize.Width,
            double.IsInfinity(availableSize.Height) ? 24d : availableSize.Height);
        return new Size(side, side);
    }

    public override void Render(DrawingContext context)
    {
        if (_geometry is null) return;
        var size = Bounds.Size;
        if (size.Width <= 0 || size.Height <= 0) return;

        // SVG 语义：几何与线宽均在 24 坐标系下，整体等比缩放并居中
        var scale = Math.Min(size.Width / 24d, size.Height / 24d);
        var tx = (size.Width - 24d * scale) / 2d;
        var ty = (size.Height - 24d * scale) / 2d;
        using var state = context.PushTransform(
            Matrix.CreateScale(scale, scale) * Matrix.CreateTranslation(tx, ty));

        var pen = new Pen(IconBrush, StrokeWidth)
        {
            LineCap = PenLineCap.Round,
            LineJoin = PenLineJoin.Round
        };
        context.DrawGeometry(null, pen, _geometry);
    }

    /// <summary>解析图标为几何（供其他控件复用；返回 null 表示不可用）。</summary>
    internal static Geometry? _Load(string? icon)
    {
        if (string.IsNullOrWhiteSpace(icon)) return null;

        // 名格式：[pack/]name（与 WPF SvgIconKey 一致；无 pack 时先试 default 再试 lucide）
        var parts = icon.Split('/', 2);
        var names = parts.Length == 2
            ? [(pack: parts[0], name: parts[1])]
            : new[] { (pack: DefaultPack, name: parts[0]), (pack: "lucide", name: parts[0]) };

        foreach (var (pack, name) in names)
            if (_LoadPackIcon(pack, name) is { } geometry)
                return geometry;
        return null;
    }

    private static Geometry? _LoadPackIcon(string pack, string name) =>
        Cache.GetOrAdd($"{pack}/{name}", _ =>
        {
            try
            {
                var uri = new Uri($"avares://PCL.CE.Avalonia/Assets/IconPacks/{pack}/{name}.svg");
                using var stream = AssetLoader.Open(uri);
                var doc = XDocument.Load(stream);

                var figures = new List<PathFigure>();
                foreach (var element in doc.Descendants().Where(e =>
                             e.Name.LocalName is "path" or "circle" or "ellipse" or "line" or "rect"
                                 or "polyline" or "polygon"))
                {
                    try
                    {
                        figures.AddRange(_ParseElement(element));
                    }
                    catch
                    {
                        // 忽略无法解析的元素
                    }
                }

                if (figures.Count == 0) return null;
                var combined = new PathGeometry { FillRule = FillRule.NonZero };
                foreach (var figure in figures) combined.Figures.Add(figure);
                return combined;
            }
            catch
            {
                return null;
            }
        });

    /// <summary>SVG 基本图形 → PathFigure 列表（lucide 除 path 外还使用 circle/line/rect 等，均以 stroke 渲染）。</summary>
    private static IEnumerable<PathFigure> _ParseElement(XElement e)
    {
        var name = e.Name.LocalName;
        if (name == "path")
            return PathGeometry.Parse(e.Attribute("d")?.Value ?? "").Figures;

        double Attr(string local) => double.Parse(e.Attribute(local)?.Value ?? "0");

        PathFigure Figure(Point start, params PathSegment[] segments)
        {
            var figure = new PathFigure { StartPoint = start, IsClosed = false };
            foreach (var segment in segments) figure.Segments.Add(segment);
            return figure;
        }

        return name switch
        {
            // 完整圆：两条半圆弧拼合（单段弧在起终点重合时会被渲染器丢弃）
            "circle" => [Figure(
                new Point(Attr("cx") + Attr("r"), Attr("cy")),
                new ArcSegment
                {
                    Size = new Size(Attr("r"), Attr("r")),
                    SweepDirection = SweepDirection.Clockwise,
                    Point = new Point(Attr("cx") - Attr("r"), Attr("cy"))
                },
                new ArcSegment
                {
                    Size = new Size(Attr("r"), Attr("r")),
                    SweepDirection = SweepDirection.Clockwise,
                    Point = new Point(Attr("cx") + Attr("r"), Attr("cy"))
                })],
            "ellipse" => [Figure(
                new Point(Attr("cx") + Attr("rx"), Attr("cy")),
                new ArcSegment
                {
                    Size = new Size(Attr("rx"), Attr("ry")),
                    SweepDirection = SweepDirection.Clockwise,
                    Point = new Point(Attr("cx") - Attr("rx"), Attr("cy"))
                },
                new ArcSegment
                {
                    Size = new Size(Attr("rx"), Attr("ry")),
                    SweepDirection = SweepDirection.Clockwise,
                    Point = new Point(Attr("cx") + Attr("rx"), Attr("cy"))
                })],
            "line" => [Figure(
                new Point(Attr("x1"), Attr("y1")),
                new LineSegment { Point = new Point(Attr("x2"), Attr("y2")) })],
            "rect" => [Figure(
                new Point(Attr("x"), Attr("y")),
                new PolyLineSegment
                {
                    Points = new List<Point>
                    {
                        new(Attr("x") + Attr("width"), Attr("y")),
                        new(Attr("x") + Attr("width"), Attr("y") + Attr("height")),
                        new(Attr("x"), Attr("y") + Attr("height"))
                    }
                })],
            "polyline" or "polygon" => _ParsePoints(e, name == "polygon"),
            _ => []
        };
    }

    private static IEnumerable<PathFigure> _ParsePoints(XElement e, bool closed)
    {
        var points = (e.Attribute("points")?.Value ?? "")
            .Split([' ', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(double.Parse)
            .ToArray();
        if (points.Length < 4) return [];

        var start = new Point(points[0], points[1]);
        var rest = new List<Point>();
        for (var i = 2; i + 1 < points.Length; i += 2)
            rest.Add(new Point(points[i], points[i + 1]));
        if (closed) rest.Add(start);
        return [new PathFigure
        {
            StartPoint = start,
            IsClosed = closed,
            Segments = { new PolyLineSegment { Points = rest } }
        }];
    }
}
