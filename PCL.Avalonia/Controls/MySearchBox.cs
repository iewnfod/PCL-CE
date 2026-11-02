using Avalonia;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;

namespace PCL.Avalonia.Controls;

/// <summary>
///     PCL 搜索框（1:1 复刻 WPF MySearchBox）：高 40 圆角 5 卡片（TransparentBackground 底 + 主题色阴影）、
///     左侧放大镜图标（14x14、ColorBrush1、边距 14）、透明无边框输入框（Padding 32,0,40,0、字号 13.5）、
///     右侧清除按钮（24x24、Black 主题 x 图标、有文本时显示）。
/// </summary>
public class MySearchBox : Border
{
    public static readonly StyledProperty<string> TextProperty =
        AvaloniaProperty.Register<MySearchBox, string>(nameof(Text), "");

    private readonly TextBox _textBox;
    private readonly MyIconButton _btnClear;
    private bool _syncing;

    public event EventHandler? SearchChanged;

    public string Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public TextBox TextBox => _textBox;

    public MySearchBox()
    {
        Height = 40;
        CornerRadius = new CornerRadius(5);
        Bind(Border.BackgroundProperty, new DynamicResourceExtension("ColorBrushTransparentBackground"));
        // 主题色阴影（与 MyCard 一致：0.07 常态）
        BoxShadow = new BoxShadows(new BoxShadow
        {
            OffsetX = 0, OffsetY = 0, Blur = 6, Spread = 2,
            Color = Color.FromArgb(51, 0, 0, 0),
        });

        _textBox = new TextBox
        {
            VerticalAlignment = VerticalAlignment.Stretch,
            VerticalContentAlignment = VerticalAlignment.Center,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(32, 0, 40, 0),
            FontSize = 13.5,
            Background = Brushes.Transparent,
            MaxLength = 50,
            Text = "",
            Watermark = PCL.Core.App.Localization.Lang.Text("Select.Instance.Search.Hint"),
        };
        _textBox.TextChanged += (_, _) =>
        {
            if (_syncing) return;
            _syncing = true;
            try
            {
                Text = _textBox.Text ?? "";
                SearchChanged?.Invoke(this, EventArgs.Empty);
            }
            finally
            {
                _syncing = false;
            }
        };

        var icon = new SvgIcon
        {
            Icon = "lucide/search",
            Width = 14,
            Height = 14,
            IsHitTestVisible = false,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(14, 0, 0, 0),
        };
        icon.Bind(SvgIcon.IconBrushProperty, new DynamicResourceExtension("ColorBrush1"));

        _btnClear = new MyIconButton
        {
            SvgIcon = "lucide/x",
            Theme = MyIconButtonTheme.Black,
            Width = 24,
            Height = 24,
            LogoScale = 0.75,
            Margin = new Thickness(0, 0, 10, 0),
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
            Opacity = 0,
            IsHitTestVisible = false,
        };
        _btnClear.Click += (_, _) => Text = "";

        Child = new Panel
        {
            Children =
            {
                _textBox,
                icon,
                _btnClear,
            }
        };

        TextProperty.Changed.AddClassHandler<MySearchBox>((box, e) =>
        {
            if (box._syncing) return;
            box._syncing = true;
            try
            {
                box._textBox.Text = (string?)e.NewValue ?? "";
                box._RefreshClear();
                box.SearchChanged?.Invoke(box, EventArgs.Empty);
            }
            finally
            {
                box._syncing = false;
            }
        });
    }

    private void _RefreshClear()
    {
        var hasText = !string.IsNullOrEmpty(Text);
        _btnClear.Opacity = hasText ? 1 : 0;
        _btnClear.IsHitTestVisible = hasText;
    }
}
