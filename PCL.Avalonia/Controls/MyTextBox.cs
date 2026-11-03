using Avalonia;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace PCL.Avalonia.Controls;

/// <summary>
///     PCL 文本输入框（复刻 WPF MyTextBox 常用态）：高 28 圆角 4、SemiTransparent 底、
///     Gray5 边框、聚焦时边框转主题色、内边距 6,0、字号 13。
/// </summary>
public class MyTextBox : Border
{
    public static readonly StyledProperty<string> TextProperty =
        AvaloniaProperty.Register<MyTextBox, string>(nameof(Text), "");

    public static readonly StyledProperty<string> WatermarkProperty =
        AvaloniaProperty.Register<MyTextBox, string>(nameof(Watermark), "");

    private readonly TextBox _textBox;
    private bool _syncing;

    /// <summary>回车按下（用于「用户名 → 登录」直达）。</summary>
    public event EventHandler? EnterPressed;

    public string Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public string Watermark
    {
        get => GetValue(WatermarkProperty);
        set => SetValue(WatermarkProperty, value);
    }

    public MyTextBox()
    {
        Height = 28;
        CornerRadius = new CornerRadius(4);
        Padding = new Thickness(0);
        Bind(Border.BackgroundProperty, new DynamicResourceExtension("ColorBrushSemiTransparent"));
        Bind(Border.BorderBrushProperty, new DynamicResourceExtension("ColorBrushGray5"));
        BorderThickness = new Thickness(1);

        _textBox = new TextBox
        {
            VerticalAlignment = VerticalAlignment.Stretch,
            VerticalContentAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(6, 0, 4, 0),
            FontSize = 13,
            Background = Brushes.Transparent,
            Text = "",
        };
        _textBox.Bind(TextBox.ForegroundProperty, new DynamicResourceExtension("ColorBrush1"));
        _textBox.KeyDown += (_, e) =>
        {
            if (e.Key is global::Avalonia.Input.Key.Enter)
            {
                EnterPressed?.Invoke(this, EventArgs.Empty);
                e.Handled = true;
            }
        };
        _textBox.TextChanged += (_, _) =>
        {
            if (_syncing) return;
            _syncing = true;
            try { Text = _textBox.Text ?? ""; }
            finally { _syncing = false; }
        };

        Child = _textBox;

        // 聚焦态：边框转主题色
        _textBox.GotFocus += (_, _) => Bind(Border.BorderBrushProperty,
            new DynamicResourceExtension("ColorBrush2"));
        _textBox.LostFocus += (_, _) => Bind(Border.BorderBrushProperty,
            new DynamicResourceExtension("ColorBrushGray5"));

        TextProperty.Changed.AddClassHandler<MyTextBox>((box, e) =>
        {
            if (box._syncing) return;
            box._syncing = true;
            try { box._textBox.Text = (string?)e.NewValue ?? ""; }
            finally { box._syncing = false; }
        });
        WatermarkProperty.Changed.AddClassHandler<MyTextBox>((box, e) =>
            box._textBox.Watermark = (string?)e.NewValue ?? "");
    }

    /// <summary>聚焦输入区。</summary>
    public void FocusInput() => _textBox.Focus();
}
