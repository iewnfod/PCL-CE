using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.LogicalTree;
using Avalonia.Media;
using System.Runtime.CompilerServices;

namespace PCL.Avalonia.Controls;

public enum MyRadioButtonColorType
{
    White,
    Highlight,
    /// <summary>页面圆点单选（WPF MyRadioBox）：透明底、14px 圆环 + 选中内点。</summary>
    RadioBox
}

/// <summary>
///     PCL 单选按钮（1:1 复刻 WPF MyRadioButton）：高 27 圆角 13.5 胶囊、图标 + 文字。<br/>
///     White：正常 SemiTransparent 底白字，悬停半透明白底，选中白底 + 主题色字（标题栏导航用）。<br/>
///     Highlight：正常 SemiTransparent 底主题色字，选中主题色底白字。<br/>
///     互斥：同一父 Panel 内或相同 GroupName 的按钮自动保证仅一个选中。
/// </summary>
[TemplatePart("PART_Text", typeof(TextBlock))]
[TemplatePart("PART_LogoHost", typeof(Panel))]
[TemplatePart("PART_Icon", typeof(SvgIcon))]
public class MyRadioButton : TemplatedControl
{
    public delegate void CheckEventHandler(MyRadioButton sender, bool raiseByMouse);

    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<MyRadioButton, string?>(nameof(Text));

    public static readonly StyledProperty<string?> SvgIconProperty =
        AvaloniaProperty.Register<MyRadioButton, string?>(nameof(SvgIcon));

    public static readonly StyledProperty<string?> LogoProperty =
        AvaloniaProperty.Register<MyRadioButton, string?>(nameof(Logo));

    public static readonly StyledProperty<double> LogoScaleProperty =
        AvaloniaProperty.Register<MyRadioButton, double>(nameof(LogoScale), 1d);

    public static readonly StyledProperty<bool> CheckedProperty =
        AvaloniaProperty.Register<MyRadioButton, bool>(nameof(Checked));

    public static readonly StyledProperty<string?> GroupNameProperty =
        AvaloniaProperty.Register<MyRadioButton, string?>(nameof(GroupName));

    public static readonly StyledProperty<MyRadioButtonColorType> ColorTypeProperty =
        AvaloniaProperty.Register<MyRadioButton, MyRadioButtonColorType>(nameof(ColorType));

    public static readonly StyledProperty<System.Windows.Input.ICommand?> CheckCommandProperty =
        AvaloniaProperty.Register<MyRadioButton, System.Windows.Input.ICommand?>(nameof(CheckCommand));

    public static readonly StyledProperty<object?> CheckCommandParameterProperty =
        AvaloniaProperty.Register<MyRadioButton, object?>(nameof(CheckCommandParameter));

    private static readonly Dictionary<string, List<WeakReference<MyRadioButton>>> GroupRegistry = new();
    private static readonly object GroupRegistryLock = new();

    private TextBlock? _text;
    private Panel? _logoHost;
    private SvgIcon? _icon;
    private bool _pressed;
    private bool _isSettingChecked;
    private bool _isUserInteraction;
    private bool _isExecutingCheckCommand;

    static MyRadioButton()
    {
        TextProperty.Changed.AddClassHandler<MyRadioButton>((c, _) => c._RefreshText());
        SvgIconProperty.Changed.AddClassHandler<MyRadioButton>((c, _) => c._RefreshLogo());
        LogoProperty.Changed.AddClassHandler<MyRadioButton>((c, _) => c._RefreshLogo());
        LogoScaleProperty.Changed.AddClassHandler<MyRadioButton>((c, _) => c._RefreshLogo());
        CheckedProperty.Changed.AddClassHandler<MyRadioButton>((c, e) =>
        {
            if (c._isSettingChecked) return;
            c.PseudoClasses.Set(":checked", (bool)e.NewValue!);
            c._EnsureSingleChecked();
        });
    }

    public MyRadioButton()
    {
        PointerPressed += _OnPointerPressed;
        PointerReleased += _OnPointerReleased;
        PointerExited += (_, _) =>
        {
            if (_pressed) ClickTrace.Log($"MyRadioButton[{Text}] Exited（按下状态被清除）");
            _pressed = false;
        };
        AttachedToVisualTree += (_, _) =>
        {
            _RefreshText();
            _RefreshLogo();
            PseudoClasses.Set(":checked", Checked);
            if (!string.IsNullOrEmpty(GroupName)) _RegisterGroup(GroupName);
            _EnsureSingleChecked();
        };
        DetachedFromLogicalTree += (_, _) =>
        {
            if (!string.IsNullOrEmpty(GroupName)) _UnregisterGroup(GroupName);
        };
    }

    public string? Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public string? SvgIcon
    {
        get => GetValue(SvgIconProperty);
        set => SetValue(SvgIconProperty, value);
    }

    /// <summary>矢量 Path 数据 Logo（WPF 兼容；与 SvgIcon 二选一，SvgIcon 优先）。</summary>
    public string? Logo
    {
        get => GetValue(LogoProperty);
        set => SetValue(LogoProperty, value);
    }

    public double LogoScale
    {
        get => GetValue(LogoScaleProperty);
        set => SetValue(LogoScaleProperty, value);
    }

    public bool Checked
    {
        get => GetValue(CheckedProperty);
        set => SetChecked(value, false);
    }

    public string? GroupName
    {
        get => GetValue(GroupNameProperty);
        set => SetValue(GroupNameProperty, value);
    }

    public MyRadioButtonColorType ColorType
    {
        get => GetValue(ColorTypeProperty);
        set => SetValue(ColorTypeProperty, value);
    }

    public System.Windows.Input.ICommand? CheckCommand
    {
        get => GetValue(CheckCommandProperty);
        set => SetValue(CheckCommandProperty, value);
    }

    public object? CheckCommandParameter
    {
        get => GetValue(CheckCommandParameterProperty);
        set => SetValue(CheckCommandParameterProperty, value);
    }

    public event CheckEventHandler? Check;

    /// <summary>手动设置选中状态（对齐 WPF SetChecked 语义）。</summary>
    public void SetChecked(bool value, bool raiseByMouse)
    {
        var isChanged = Checked != value;
        _isUserInteraction = raiseByMouse;
        try
        {
            if (isChanged)
            {
                _isSettingChecked = true;
                try
                {
                    SetValue(CheckedProperty, value);
                    PseudoClasses.Set(":checked", value);
                }
                finally
                {
                    _isSettingChecked = false;
                }
            }

            if (Parent is null && string.IsNullOrEmpty(GroupName)) return;
            _EnsureSingleChecked();
            if (!isChanged) return;

            if (Checked) Check?.Invoke(this, raiseByMouse);
        }
        finally
        {
            _isUserInteraction = false;
        }
    }

    private void _OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        var left = e.GetCurrentPoint(this).Properties.IsLeftButtonPressed;
        ClickTrace.Log($"MyRadioButton[{Text}] Pressed 左键={left} checked={Checked}");
        if (!Checked && left)
            _pressed = true;
    }

    private void _OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        ClickTrace.Log($"MyRadioButton[{Text}] Released _pressed={_pressed} checked={Checked} 位置={e.GetPosition(this)} 边界={Bounds} 尺寸={Bounds.Width}x{Bounds.Height}");
        if (Checked || !_pressed) return;
        _pressed = false;
        if (!new Rect(0, 0, Bounds.Width, Bounds.Height).Contains(e.GetPosition(this))) return;
        SetChecked(true, true);
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _text = e.NameScope.Find<TextBlock>("PART_Text");
        _logoHost = e.NameScope.Find<Panel>("PART_LogoHost");
        _icon = e.NameScope.Find<SvgIcon>("PART_Icon");
        _RefreshText();
        _RefreshLogo();
    }

    private void _RefreshText()
    {
        if (_text is not null) _text.Text = Text;
    }

    private void _RefreshLogo()
    {
        if (_logoHost is null) return;
        var hasSvg = !string.IsNullOrWhiteSpace(SvgIcon);
        var hasLogo = !string.IsNullOrWhiteSpace(Logo);
        _logoHost.Width = hasSvg || hasLogo ? 16 : 0;
        if (_icon is not null)
        {
            _icon.Icon = hasSvg ? SvgIcon : null;
            _icon.IsVisible = hasSvg;
            if (!hasSvg && double.IsNaN(_icon.Width)) _icon.Width = double.NaN;
        }

        // 矢量 Logo 由主题模板外置（MVP 用 SvgIcon 即可覆盖标题栏场景）
        var scale = hasSvg ? 1d : LogoScale;
        _logoHost.RenderTransform = scale.Equals(1d)
            ? null
            : new ScaleTransform(scale, scale);
        _logoHost.RenderTransformOrigin = new RelativePoint(0.5, 0.5, RelativeUnit.Relative);
    }

    #region 互斥（父 Panel / GroupName）

    private void _EnsureSingleChecked()
    {
        if (!string.IsNullOrEmpty(GroupName))
        {
            if (!Checked) return;
            lock (GroupRegistryLock)
            {
                if (!GroupRegistry.TryGetValue(GroupName!, out var members)) return;
                foreach (var reference in members.ToArray())
                    if (reference.TryGetTarget(out var other) && !ReferenceEquals(other, this))
                        other.SetChecked(false, false);
            }

            return;
        }

        if (Parent is not Panel parentPanel) return;
        var radios = parentPanel.Children.OfType<MyRadioButton>().ToList();
        if (radios.Count == 0) return;
        var checkedCount = radios.Count(r => r.Checked);
        switch (checkedCount)
        {
            case 0:
                radios[0].Checked = true;
                break;
            case > 1:
                if (Checked)
                    foreach (var radio in radios.Where(r => r.Checked && !ReferenceEquals(r, this)))
                        radio.Checked = false;
                else
                {
                    var firstSeen = false;
                    foreach (var radio in radios.Where(r => r.Checked))
                    {
                        if (firstSeen) radio.Checked = false;
                        else firstSeen = true;
                    }
                }

                break;
        }
    }

    private void _RegisterGroup(string? groupName)
    {
        if (string.IsNullOrEmpty(groupName)) return;
        lock (GroupRegistryLock)
        {
            if (!GroupRegistry.TryGetValue(groupName!, out var members))
            {
                members = [];
                GroupRegistry[groupName!] = members;
            }

            members.RemoveAll(r => !r.TryGetTarget(out _));
            members.RemoveAll(r => r.TryGetTarget(out var t) && ReferenceEquals(t, this));
            members.Add(new WeakReference<MyRadioButton>(this));
        }
    }

    private void _UnregisterGroup(string? groupName)
    {
        if (string.IsNullOrEmpty(groupName)) return;
        lock (GroupRegistryLock)
        {
            if (!GroupRegistry.TryGetValue(groupName!, out var members)) return;
            members.RemoveAll(r => !r.TryGetTarget(out _));
            members.RemoveAll(r => r.TryGetTarget(out var t) && ReferenceEquals(t, this));
            if (members.Count == 0) GroupRegistry.Remove(groupName!);
        }
    }

    #endregion
}
