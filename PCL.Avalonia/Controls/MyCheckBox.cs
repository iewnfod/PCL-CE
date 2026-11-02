using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using PathShape = Avalonia.Controls.Shapes.Path;

namespace PCL.Avalonia.Controls;

/// <summary>
///     PCL 复选框（1:1 复刻 WPF MyCheckBox）：18×18 圆角 3 方框（边 1.1）+ 12×12 对勾 + 10×10 不定态方块。<br/>
///     文字 13px、Margin 左 26；悬停文字/边框 ColorBrush3（100/200ms）、选中边框 ColorBrush2、
///     按下方框缩至 16.5 且底色 Bg1、对勾 300ms 弹性展开。支持两态/三态。
/// </summary>
public class MyCheckBox : TemplatedControl
{
    public delegate void ChangeEventHandler(object? sender, bool user);

    public delegate void PreviewChangeEventHandler(object? sender, RouteEventArgs e);

    public static readonly StyledProperty<bool?> CheckedProperty =
        AvaloniaProperty.Register<MyCheckBox, bool?>(nameof(Checked), false,
            defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<bool> IsThreeStateProperty =
        AvaloniaProperty.Register<MyCheckBox, bool>(nameof(IsThreeState));

    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<MyCheckBox, string?>(nameof(Text));

    public static readonly StyledProperty<System.Windows.Input.ICommand?> CheckCommandProperty =
        AvaloniaProperty.Register<MyCheckBox, System.Windows.Input.ICommand?>(nameof(CheckCommand));

    public static readonly StyledProperty<object?> CheckCommandParameterProperty =
        AvaloniaProperty.Register<MyCheckBox, object?>(nameof(CheckCommandParameter));

    private bool _isSettingChecked;
    private bool _isUserInteraction;
    private bool _isExecutingCheckCommand;
    private bool _pressed;

    /// <summary>勾选状态（true/false/null 不定态）。</summary>
    public bool? Checked
    {
        get => GetValue(CheckedProperty);
        set => SetChecked(value, false);
    }

    /// <summary>是否为三态复选框。</summary>
    public bool IsThreeState
    {
        get => GetValue(IsThreeStateProperty);
        set => SetValue(IsThreeStateProperty, value);
    }

    public string? Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
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

    /// <summary>勾选状态改变（user 标记是否由用户点击引发）。</summary>
    public event ChangeEventHandler? Change;

    /// <summary>勾选前预览事件；置 Handled 可取消本次勾选。</summary>
    public event PreviewChangeEventHandler? PreviewChange;

    static MyCheckBox()
    {
        CheckedProperty.Changed.AddClassHandler<MyCheckBox>((c, e) =>
        {
            if (c._isSettingChecked) return;
            c._SyncState();
            if (c._isUserInteraction) c._ExecuteCheckCommand();
        });
    }

    public MyCheckBox()
    {
        Focusable = true;
        PointerPressed += _OnPointerPressed;
        PointerReleased += _OnPointerReleased;
        PointerExited += _OnPointerExited;
    }

    /// <summary>手动设置勾选状态（对齐 WPF SetChecked：PreviewChange 可取消）。</summary>
    public void SetChecked(bool? value, bool user)
    {
        if (Checked is { } oldValue && value is { } newValue && oldValue == newValue) return;

        if (value is true && user)
        {
            var args = new RouteEventArgs(user);
            PreviewChange?.Invoke(this, args);
            if (args.Handled) return;
        }

        var final = IsThreeState ? value : value == true;
        _isUserInteraction = user;
        try
        {
            _isSettingChecked = true;
            SetValue(CheckedProperty, final);
        }
        finally
        {
            _isSettingChecked = false;
            _isUserInteraction = false;
        }

        _SyncState();
        Change?.Invoke(this, user);
    }

    private void _SyncState()
    {
        PseudoClasses.Set(":checked", Checked == true);
        PseudoClasses.Set(":indeterminate", Checked is null);
    }

    private void _ExecuteCheckCommand()
    {
        var command = CheckCommand;
        if (_isExecutingCheckCommand || command is null || command.CanExecute(CheckCommandParameter) != true)
            return;
        _isExecutingCheckCommand = true;
        try
        {
            command.Execute(CheckCommandParameter);
        }
        finally
        {
            _isExecutingCheckCommand = false;
        }
    }

    private void _OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        _pressed = true;
        PseudoClasses.Set(":pressed", true);
        Focus();
    }

    private void _OnPointerExited(object? sender, PointerEventArgs e)
    {
        _pressed = false;
        PseudoClasses.Set(":pressed", false);
    }

    private void _OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (!_pressed) return;
        _pressed = false;
        PseudoClasses.Set(":pressed", false);
        if (!new Rect(0, 0, Bounds.Width, Bounds.Height).Contains(e.GetPosition(this))) return;

        // 三态循环：false → true → null → false（WPF Checkbox_MouseUp 同款）
        if (IsThreeState)
            switch (Checked)
            {
                case true:
                    SetChecked(null, true);
                    break;
                case false:
                    SetChecked(true, true);
                    break;
                case null:
                    SetChecked(false, true);
                    break;
            }
        else
            SetChecked(!Checked, true);
    }
}
