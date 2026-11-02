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
        _layer = new Panel
        {
            VerticalAlignment = VerticalAlignment.Bottom,
            HorizontalAlignment = HorizontalAlignment.Center,
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
        // 自底向上堆叠
        for (var i = 0; i < _activeBars.Count; i++)
            _activeBars[i].Margin = new Thickness(0, 0, 0, i * 52);
    }

    /// <summary>单条提示栏：圆角条 + 文本 + 关闭按钮；错误红底、常规主题色底。</summary>
    private sealed class HintBar : Border
    {
        public HintViewModel Hint { get; }

        public HintBar(HintViewModel hint, Action<HintViewModel> dismiss)
        {
            Hint = hint;
            CornerRadius = new CornerRadius(6);
            Padding = new Thickness(14, 10);
            MinWidth = 160;
            MaxWidth = 480;
            ZIndex = 1000;
            IsHitTestVisible = true;
            BoxShadow = BoxShadows.Parse("0 2 6 0");

            var isError = hint.Kind == HintKind.Error;
            var text = new TextBlock
            {
                Text = hint.Message,
                TextWrapping = TextWrapping.Wrap,
                MaxLines = 3,
            };
            var dismissButton = new Button
            {
                Content = "✕",
                FontSize = 11,
                Padding = new Thickness(4),
                MinHeight = 22,
                VerticalAlignment = VerticalAlignment.Top,
            };
            dismissButton.Click += (_, _) => dismiss(hint);

            Child = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 10,
                Children = { text, dismissButton }
            };

            // 类型配色：错误浅红底；其余用主题色 ColorBrush1 + 白字
            var app = Application.Current;
            if (isError)
            {
                Background = new ImmutableSolidColorBrush(Color.Parse("#FDECEC"));
                text.Foreground = new ImmutableSolidColorBrush(Color.Parse("#C0392B"));
            }
            else if (app?.Resources.TryGetResource("ColorBrush1", app.ActualThemeVariant, out var back) == true &&
                     back is IBrush brush)
            {
                Background = brush;
                text.Foreground = Brushes.White;
            }
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
        // 覆盖层：铺满窗口（pointer 穿透），提示条在层内自排版
        if (window.Content is Panel root)
            root.Children.Add(presenter);
        HintService.Instance.Presenter = presenter;
    }
}
