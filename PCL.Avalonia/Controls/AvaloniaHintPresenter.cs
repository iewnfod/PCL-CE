using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Threading;
using PCL.Core.UI.Hints;

namespace PCL.Avalonia.Controls;

/// <summary>
///     <see cref="IHintPresenter" /> 的 Avalonia 实现：在主窗口底部中央显示浮动提示条，
///     对应 WPF 版的 HintPresenter + MyHint。由 <see cref="HintPresenterBridge"/> 装配。
/// </summary>
public class AvaloniaHintPresenter : Panel, IHintPresenter
{
    private readonly Panel _layer;
    private readonly List<HintBar> _activeBars = [];

    public bool IsAvailable => true;

    public AvaloniaHintPresenter()
    {
        IsHitTestVisible = false;
        _layer = new StackPanel
        {
            Orientation = Orientation.Vertical,
            Spacing = 8,
            VerticalAlignment = VerticalAlignment.Bottom,
            // 对齐 WPF：右下角堆叠、右缘对齐，留白 18/20
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 0, 18, 20),
        };
        Children.Add(_layer);
    }

    public void Present(HintViewModel hint)
    {
        Dispatcher.UIThread.Post(() =>
        {
            var bar = new HintBar(hint, Dismiss);
            _activeBars.Add(bar);
            _layer.Children.Add(bar);
            _Relayout();
        });
    }

    public void Dismiss(HintViewModel hint)
    {
        Dispatcher.UIThread.Post(() =>
        {
            var bar = _activeBars.FirstOrDefault(b => ReferenceEquals(b.Hint, hint));
            if (bar is null) return;
            _activeBars.Remove(bar);
            _layer.Children.Remove(bar);
            _Relayout();
            hint.Close();
        });
    }

    public void DismissAll()
    {
        foreach (var bar in _activeBars.ToArray()) Dismiss(bar.Hint);
    }

    private void _Relayout()
    {
        // StackPanel 自动堆叠，无需手动定位
    }

    /// <summary>单条提示栏：圆角条 + 文本 + 关闭按钮；错误红底、常规主题色底。</summary>
    private sealed class HintBar : Border
    {
        public HintViewModel Hint { get; }

        public HintBar(HintViewModel hint, Action<HintViewModel> dismiss)
        {
            Hint = hint;
            CornerRadius = new CornerRadius(6);
            Padding = new Thickness(14, 8);
            MinWidth = 160;
            MaxWidth = 480;
            ZIndex = 1000;
            IsHitTestVisible = true;
            BoxShadow = new BoxShadows(new BoxShadow { OffsetY = 2, Blur = 6 });

            var kind = hint.Kind;
            var text = new TextBlock
            {
                Text = hint.Message,
                TextWrapping = TextWrapping.Wrap,
                MaxLines = 3,
                VerticalAlignment = VerticalAlignment.Center,
            };
            // 统一白底；不同类型仅左侧圆形图标不同（×/✓/!/i）
            var (iconColor, iconName, foreColor) = kind switch
            {
                HintKind.Error => ("#D9534F", "lucide/x", "#C0392B"),
                HintKind.Success => ("#2E9E5B", "lucide/circle-check", "#1E7A43"),
                HintKind.Warning => ("#E8A23D", "lucide/triangle-alert", "#9A6A15"),
                _ => ("#3B82F6", "lucide/info", "#1D5FCC"),
            };
            var prefix = new Border
            {
                Width = 18,
                Height = 18,
                CornerRadius = new CornerRadius(9),
                Background = new ImmutableSolidColorBrush(Color.Parse(iconColor)),
                VerticalAlignment = VerticalAlignment.Center,
                Child = new SvgIcon
                {
                    Icon = iconName,
                    Width = 10,
                    Height = 10,
                    IconBrush = Brushes.White,
                },
            };
            // 轻量关闭按钮（WPF MyHint 同款小尺寸，避免 Fluent 默认 32 高把提示条撑高）
            var dismissButton = new Button
            {
                Content = "✕",
                FontSize = 11,
                Padding = new Thickness(4, 1),
                MinHeight = 20,
                MinWidth = 20,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Right,
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
            };
            dismissButton.Click += (_, _) => dismiss(hint);

            // 统一白底 + 类型色文字
            Background = new ImmutableSolidColorBrush(Color.Parse("#FFFFFF"));
            text.Foreground = new ImmutableSolidColorBrush(Color.Parse(foreColor));
            dismissButton.Foreground = new ImmutableSolidColorBrush(Color.Parse("#8A8A8A"));

            var contentPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 10,
                Children = { text, dismissButton }
            };
            // 图标在左、内容在右
            Child = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 10,
                VerticalAlignment = VerticalAlignment.Center,
                Children = { prefix, contentPanel }
            };

            // 自动消失（对齐 WPF hint 行为：普通 6s / 错误 9s，避免提示条遮挡界面）
            var timeout = kind == HintKind.Error ? 9 : 6;
            var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(timeout) };
            timer.Tick += (_, _) =>
            {
                timer.Stop();
                dismiss(hint);
            };
            timer.Start();
            DetachedFromVisualTree += (_, _) => timer.Stop();
        }
    }
}

/// <summary>把 <see cref="HintService" /> 接到 Avalonia 呈现器。在主窗口就绪后调用。</summary>
public static class HintPresenterBridge
{
    public static void Install(Window window)
    {
        if (HintService.Instance.Presenter is AvaloniaHintPresenter) return;
        var presenter = new AvaloniaHintPresenter();
        // 覆盖层：铺满窗口（pointer 穿透），提示条在层内自排版。
        // 根布局是 Grid（48,*）时必须铺满全部行，否则提示条被限制在标题栏行内（显示在右上角）
        if (window.Content is Panel root)
        {
            root.Children.Add(presenter);
            if (root is global::Avalonia.Controls.Grid grid)
            {
                var rows = Math.Max(grid.RowDefinitions.Count, 1);
                global::Avalonia.Controls.Grid.SetRow(presenter, 0);
                global::Avalonia.Controls.Grid.SetRowSpan(presenter, rows);
            }

            presenter.SetValue(global::Avalonia.Controls.Panel.ZIndexProperty, 1000);
        }

        HintService.Instance.Presenter = presenter;
    }
}
