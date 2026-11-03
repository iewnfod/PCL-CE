using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;

namespace PCL.Avalonia.Controls;

/// <summary>
///     PCL 下拉框（复刻 WPF MyComboBox 选择模式）：27px 高圆角 4、SemiTransparent 底、
///     悬停边框转主题色、右侧 V 形箭头；弹层为白底卡片（阴影 + 选中项主题色高亮）。
/// </summary>
public class MyComboBox : Border
{
    /// <summary>选项变更（user 区分用户选择与程序赋值）。</summary>
    public event Action<MyComboBox, int, bool>? SelectionChanged;

    private readonly TextBlock _text = new()
    {
        FontSize = 12.5,
        VerticalAlignment = VerticalAlignment.Center,
        TextTrimming = TextTrimming.CharacterEllipsis,
    };

    private readonly SvgIcon _arrow = new()
    {
        Icon = "lucide/chevron-right", // 图标包无 chevron-down，右箭头旋转 90° 等价
        Width = 14,
        Height = 14,
        VerticalAlignment = VerticalAlignment.Center,
        Margin = new Thickness(5, 0, 0, 0),
        IsHitTestVisible = false,
    };

    private readonly Popup _popup = new()
    {
        PlacementMode = PlacementMode.Bottom,
        IsLightDismissEnabled = true,
        WindowManagerAddShadowHint = false,
    };

    private readonly Border _popupCard = new()
    {
        CornerRadius = new CornerRadius(5),
        BorderThickness = new Thickness(1),
        MaxHeight = 300,
        MinWidth = 120,
        Child = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Content = new StackPanel(),
        },
    };

    private IReadOnlyList<string> _items = [];
    private int _selectedIndex = -1;
    private bool _isUpdatingSelection;

    public MyComboBox()
    {
        MinHeight = 27;
        CornerRadius = new CornerRadius(4);
        BorderThickness = new Thickness(1);
        Padding = new Thickness(9, 4, 24, 4);
        Cursor = new Cursor(StandardCursorType.Hand);

        this.SetDynamic(Border.BackgroundProperty, "ColorBrushSemiTransparent");
        this.SetDynamic(Border.BorderBrushProperty, "ColorBrushGray5");
        _text.SetDynamic(TextBlock.ForegroundProperty, "ColorBrushMemory");
        _arrow.SetDynamic(SvgIcon.IconBrushProperty, "ColorBrushGray4");
        _popupCard.SetDynamic(Border.BackgroundProperty, "ColorBrushWhite");
        _popupCard.SetDynamic(Border.BorderBrushProperty, "ColorBrushBg1");
        _popupCard.BoxShadow = new BoxShadows(new BoxShadow { Blur = 6, OffsetY = 2, Color = Color.FromArgb(64, 0, 0, 0) });
        _arrow.RenderTransform = new RotateTransform(90);
        _arrow.RenderTransformOrigin = new RelativePoint(0.5, 0.5, RelativeUnit.Relative);

        _popup.Child = _popupCard;
        _popup.PlacementTarget = this;

        Child = new Panel
        {
            Children =
            {
                new Grid
                {
                    ColumnDefinitions = new ColumnDefinitions("*,Auto"),
                    Children =
                    {
                        _text.WithColumn(0),
                        _arrow.WithColumn(1),
                    },
                },
                _popup,
            },
        };

        PointerPressed += _OnPointerPressed;
        PointerWheelChanged += (_, e) => e.Handled = true;
        PointerEntered += (_, _) => this.SetDynamic(Border.BorderBrushProperty, "ColorBrush1");
        PointerExited += (_, _) =>
        {
            if (!_popup.IsOpen)
                this.SetDynamic(Border.BorderBrushProperty, "ColorBrushGray5");
        };
        _popup.Closed += (_, _) =>
        {
            this.SetDynamic(Border.BorderBrushProperty, "ColorBrushGray5");
            if (_arrow.RenderTransform is RotateTransform rotate) rotate.Angle = 90;
        };
        _popup.Opened += (_, _) =>
        {
            if (_arrow.RenderTransform is RotateTransform rotate) rotate.Angle = 270;
        };
    }

    /// <summary>选项文本列表。</summary>
    public IReadOnlyList<string> Items
    {
        get => _items;
        set
        {
            _items = value ?? [];
            if (_selectedIndex >= _items.Count) SetSelectedInt(_items.Count - 1);
            _RefreshText();
            _RebuildPopupItems();
        }
    }

    /// <summary>选中项序号（-1 未选）。</summary>
    public int SelectedIndex
    {
        get => _selectedIndex;
        set
        {
            var clamped = Math.Clamp(value, -1, _items.Count - 1);
            if (clamped == _selectedIndex) return;
            _selectedIndex = clamped;
            _RefreshText();
            _RebuildPopupItems();
            if (_isUpdatingSelection) return;
            SelectionChanged?.Invoke(this, clamped, false);
        }
    }

    /// <summary>选择项（raiseEvent=false 时不触发事件，供初始化）。</summary>
    public void SetSelected(int index, bool raiseEvent)
    {
        _isUpdatingSelection = !raiseEvent;
        try
        {
            SelectedIndex = index;
        }
        finally
        {
            _isUpdatingSelection = false;
        }
    }

    private void SetSelectedInt(int index) => _selectedIndex = index;

    private void _RefreshText()
    {
        _text.Text = _selectedIndex >= 0 && _selectedIndex < _items.Count ? _items[_selectedIndex] : "";
    }

    private void _RebuildPopupItems()
    {
        if (_popupCard.Child is not ScrollViewer { Content: StackPanel panel }) return;
        panel.Children.Clear();
        for (var i = 0; i < _items.Count; i++)
        {
            var index = i;
            var label = new TextBlock
            {
                FontSize = 12.5,
                Text = _items[i],
                TextWrapping = TextWrapping.Wrap,
            };
            var row = new Border
            {
                Padding = new Thickness(9, 5),
                Child = label,
                Cursor = new Cursor(StandardCursorType.Hand),
                MinWidth = Bounds.Width,
            };
            if (i == _selectedIndex)
            {
                row.SetDynamic(Border.BackgroundProperty, "ColorBrushBg1");
                label.SetDynamic(TextBlock.ForegroundProperty, "ColorBrush1");
            }
            else
            {
                row.SetDynamic(Border.BackgroundProperty, "ColorBrushTransparent");
                label.SetDynamic(TextBlock.ForegroundProperty, "ColorBrushMemory");
            }
            row.PointerPressed += (_, e) =>
            {
                e.Handled = true;
                _popup.IsOpen = false;
                _isUpdatingSelection = true;
                try
                {
                    SelectedIndex = index;
                }
                finally
                {
                    _isUpdatingSelection = false;
                }
                SelectionChanged?.Invoke(this, index, true);
            };
            row.PointerEntered += (_, _) =>
            {
                if (index != _selectedIndex)
                    row.SetDynamic(Border.BackgroundProperty, "ColorBrushSemiTransparent");
            };
            row.PointerExited += (_, _) =>
            {
                if (index != _selectedIndex)
                    row.SetDynamic(Border.BackgroundProperty, "ColorBrushTransparent");
            };
            panel.Children.Add(row);
        }
    }

    private void _OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!IsEnabled) return;
        if (_popup.IsOpen)
        {
            _popup.IsOpen = false;
            return;
        }
        _RebuildPopupItems();
        _popup.IsOpen = true;
        e.Handled = true;
    }
}

internal static class MyComboBoxExtensions
{
    internal static Control WithColumn(this Control control, int column)
    {
        Grid.SetColumn(control, column);
        return control;
    }
}
