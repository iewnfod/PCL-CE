using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Threading;
using PCL.Core.Logging;

namespace PCL.Avalonia.Controls;

/// <summary>
///     消息弹窗（对应 WPF 版 MyMsgBox 的常用子集）。<br/>
///     同步 API（<see cref="Show"/>）供业务线程阻塞等待用户选择；
///     在 UI 线程请使用 <see cref="ShowAsync"/>。
/// </summary>
public static class MyMsgBox
{
    /// <summary>显示弹窗并阻塞当前线程直到用户选择（勿在 UI 线程调用）。</summary>
    public static int Show(string text, string? title = null, IReadOnlyList<string>? buttons = null,
        bool isWarn = false)
    {
        if (Dispatcher.UIThread.CheckAccess())
        {
            LogWrapper.Warn("MyMsgBox", "在 UI 线程同步调用 Show 会死锁，已改为异步显示");
            _ = ShowAsync(text, title, buttons, isWarn);
            return 1;
        }

        var task = Dispatcher.UIThread.InvokeAsync(() => ShowAsync(text, title, buttons, isWarn));
        return task.GetAwaiter().GetResult();
    }

    /// <summary>显示弹窗，异步返回用户选择的按钮序号（1 起）。</summary>
    public static Task<int> ShowAsync(string text, string? title = null, IReadOnlyList<string>? buttons = null,
        bool isWarn = false)
    {
        var options = buttons ?? ["确定"];
        var tcs = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);

        Dispatcher.UIThread.Post(() =>
        {
            var owner = (Application.Current?.ApplicationLifetime
                as IClassicDesktopStyleApplicationLifetime)?.MainWindow;

            var overlay = new Border
            {
                Background = new ImmutableSolidColorBrush(Color.FromUInt32(0x55000000)),
                ZIndex = 2000,
                Child = new Panel()
            };

            var dialog = _BuildDialog(text, title, options, isWarn, index =>
            {
                overlay.IsEnabled = false;
                if (owner?.Content is Panel root) root.Children.Remove(overlay);
                tcs.TrySetResult(index);
            });

            ((Panel)overlay.Child!).Children.Add(new Panel
            {
                Children = { dialog }
            });
            dialog.HorizontalAlignment = HorizontalAlignment.Center;
            dialog.VerticalAlignment = VerticalAlignment.Center;

            if (owner?.Content is Panel host)
            {
                overlay.IsHitTestVisible = true;
                host.Children.Add(overlay);
            }
            else
            {
                // 无宿主窗口时退化为控制台输出
                Console.WriteLine($"[MyMsgBox] {title}: {text}");
                tcs.TrySetResult(1);
            }
        });

        return tcs.Task;
    }

    private static Border _BuildDialog(string text, string? title, IReadOnlyList<string> options,
        bool isWarn, Action<int> onChoose)
    {
        var stack = new StackPanel { Spacing = 14 };

        if (!string.IsNullOrEmpty(title))
            stack.Children.Add(new TextBlock
            {
                Text = title,
                FontSize = 16,
                FontWeight = FontWeight.Bold,
            });
        stack.Children.Add(new TextBlock
        {
            Text = text,
            TextWrapping = TextWrapping.Wrap,
            MaxWidth = 420,
        });

        var buttonPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 10,
            HorizontalAlignment = HorizontalAlignment.Right,
        };
        for (var i = 0; i < options.Count; i++)
        {
            var index = i + 1;
            var button = new Button { Content = options[i], MinWidth = 88 };
            if (i == 0) button.Classes.Add("primary");
            button.Click += (_, _) => onChoose(index);
            buttonPanel.Children.Add(button);
        }
        stack.Children.Add(buttonPanel);

        var dialog = new Border
        {
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(22, 18),
            MinWidth = 320,
            MaxWidth = 520,
            BoxShadow = BoxShadows.Parse("0 8 24 0 #66000000"),
            Child = stack,
        };
        // 背景跟随主题资源
        var app = Application.Current;
        if (app?.Resources.TryGetResource("ColorBrushBackground", app.ActualThemeVariant, out var bg) == true &&
            bg is IBrush brush)
            dialog.Background = brush;
        else
            dialog.Background = Brushes.White;
        return dialog;
    }
}
