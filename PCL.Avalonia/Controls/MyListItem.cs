using System.Collections.ObjectModel;
using System.Net.Http;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Media.Immutable;
using Avalonia.Threading;
using PathShape = Avalonia.Controls.Shapes.Path;
using SvgIconControl = PCL.Avalonia.Controls.SvgIcon;

namespace PCL.Avalonia.Controls;

public enum MyListItemCheckType
{
    None,
    Clickable,
    RadioBox,
    CheckBox
}

/// <summary>
///     PCL 列表项（1:1 复刻 WPF MyListItem）：高 42、悬停 Bg1 背景浮层、按下 ColorBrush6 + 0.98 缩放、
///     左侧勾选条（选中弹出，高 20、ColorBrush3）、RadioBox 父容器互斥、右侧按钮悬停淡入。<br/>
///     Logo 支持：SvgIcon 名 / http(s) URL / 本地位图路径 / Path 矢量数据。
/// </summary>
public class MyListItem : TemplatedControl
{
    public delegate void ClickEventHandler(object? sender, PointerReleasedEventArgs e);

    public delegate void LogoClickEventHandler(object? sender, PointerReleasedEventArgs e);

    public delegate void CheckEventHandler(object? sender, RouteEventArgs e);

    public delegate void ChangedEventHandler(object? sender, RouteEventArgs e);

    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<MyListItem, string?>(nameof(Title));

    public static readonly StyledProperty<string?> InfoProperty =
        AvaloniaProperty.Register<MyListItem, string?>(nameof(Info));

    public static readonly StyledProperty<string?> LogoProperty =
        AvaloniaProperty.Register<MyListItem, string?>(nameof(Logo));

    public static readonly StyledProperty<string?> SvgIconProperty =
        AvaloniaProperty.Register<MyListItem, string?>(nameof(SvgIcon));

    public static readonly StyledProperty<double> LogoScaleProperty =
        AvaloniaProperty.Register<MyListItem, double>(nameof(LogoScale), 1d);

    public static readonly StyledProperty<MyListItemCheckType> TypeProperty =
        AvaloniaProperty.Register<MyListItem, MyListItemCheckType>(nameof(Type));

    public static readonly StyledProperty<bool> CheckedProperty =
        AvaloniaProperty.Register<MyListItem, bool>(nameof(Checked), defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<double> FontSizeOverrideProperty =
        AvaloniaProperty.Register<MyListItem, double>(nameof(FontSizeOverride), 14d);

    public static readonly StyledProperty<int> PaddingLeftProperty =
        AvaloniaProperty.Register<MyListItem, int>(nameof(PaddingLeft));

    public static readonly StyledProperty<bool> IsScaleAnimationEnabledProperty =
        AvaloniaProperty.Register<MyListItem, bool>(nameof(IsScaleAnimationEnabled), true);

    public static readonly StyledProperty<System.Windows.Input.ICommand?> CommandProperty =
        AvaloniaProperty.Register<MyListItem, System.Windows.Input.ICommand?>(nameof(Command));

    public static readonly StyledProperty<object?> CommandParameterProperty =
        AvaloniaProperty.Register<MyListItem, object?>(nameof(CommandParameter));

    public static readonly StyledProperty<System.Windows.Input.ICommand?> CheckCommandProperty =
        AvaloniaProperty.Register<MyListItem, System.Windows.Input.ICommand?>(nameof(CheckCommand));

    public static readonly StyledProperty<object?> CheckCommandParameterProperty =
        AvaloniaProperty.Register<MyListItem, object?>(nameof(CheckCommandParameter));

    private static readonly HttpClient Http = new();

    private static readonly Dictionary<string, IImage> ImageCache = new();

    /// <summary>右侧按钮集合（悬停淡入；按钮默认 25x25）。</summary>
    public IReadOnlyList<MyIconButton>? Buttons { get; set; }

    /// <summary>右边距最小值（存在右侧按钮时悬停自动扩为 5 + 按钮数 × 25）。</summary>
    public int MinPaddingRight { get; set; } = 4;

    /// <summary>标签（本地化键，"a|b" 或列表；空项忽略、按文本排序）。</summary>
    public object? Tags { get; set; }

    /// <summary>已成型的标签文本（不做本地化求值）。</summary>
    public IEnumerable<string>? PlainTags { get; set; }

    /// <summary>Logo 是否可点击（触发 LogoClick）。</summary>
    public bool LogoClickable { get; set; }

    public string? Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value?.Replace("\r", "").Replace("\n", ""));
    }

    public string? Info
    {
        get => GetValue(InfoProperty);
        set => SetValue(InfoProperty, value?.Replace("\r", "").Replace("\n", ""));
    }

    public string? Logo
    {
        get => GetValue(LogoProperty);
        set => SetValue(LogoProperty, value);
    }

    public string? SvgIcon
    {
        get => GetValue(SvgIconProperty);
        set => SetValue(SvgIconProperty, value);
    }

    public double LogoScale
    {
        get => GetValue(LogoScaleProperty);
        set => SetValue(LogoScaleProperty, value);
    }

    public MyListItemCheckType Type
    {
        get => GetValue(TypeProperty);
        set => SetValue(TypeProperty, value);
    }

    public bool Checked
    {
        get => GetValue(CheckedProperty);
        set => SetChecked(value, false, value != Checked);
    }

    /// <summary>标题字号（WPF 版同名属性，默认 14）。</summary>
    public double FontSizeOverride
    {
        get => GetValue(FontSizeOverrideProperty);
        set => SetValue(FontSizeOverrideProperty, value);
    }

    public int PaddingLeft
    {
        get => GetValue(PaddingLeftProperty);
        set => SetValue(PaddingLeftProperty, value);
    }

    public bool IsScaleAnimationEnabled
    {
        get => GetValue(IsScaleAnimationEnabledProperty);
        set => SetValue(IsScaleAnimationEnabledProperty, value);
    }

    public System.Windows.Input.ICommand? Command
    {
        get => GetValue(CommandProperty);
        set => SetValue(CommandProperty, value);
    }

    public object? CommandParameter
    {
        get => GetValue(CommandParameterProperty);
        set => SetValue(CommandParameterProperty, value);
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

    public event ClickEventHandler? Click;
    public event LogoClickEventHandler? LogoClick;
    public event CheckEventHandler? Check;
    public event ChangedEventHandler? Changed;

    private Grid? _grid;
    private Border? _back;
    private Border? _checkBar;
    private ContentPresenter? _logoHost;
    private Panel? _buttonsHost;
    private Panel? _tagsHost;
    private TextBlock? _title;
    private TextBlock? _info;
    private Control? _logoControl;
    private bool _isSettingChecked;
    private bool _isUserInteraction;
    private bool _isExecutingCheckCommand;
    private bool _isCommandRunning;
    private bool _pressed;
    private bool _logoDown;

    static MyListItem()
    {
        InfoProperty.Changed.AddClassHandler<MyListItem>((c, _) => c._RefreshInfoVisibility());
        LogoProperty.Changed.AddClassHandler<MyListItem>((c, _) => c._UpdateLogo());
        SvgIconProperty.Changed.AddClassHandler<MyListItem>((c, _) => c._UpdateLogo());
        LogoScaleProperty.Changed.AddClassHandler<MyListItem>((c, _) => c._UpdateLogo());
        CheckedProperty.Changed.AddClassHandler<MyListItem>((c, e) =>
        {
            if (c._isSettingChecked) return;
            // 外部直接设置 Checked：与 WPF 版一致，做单选互斥并刷新视觉
            c.PseudoClasses.Set(":checked", (bool)e.NewValue!);
            c._EnsureSingleChecked();
            c._isUserInteraction = false;
        });
        TypeProperty.Changed.AddClassHandler<MyListItem>((c, _) => c._ApplyLayout());
        PaddingLeftProperty.Changed.AddClassHandler<MyListItem>((c, _) => c._ApplyLayout());
        IsScaleAnimationEnabledProperty.Changed.AddClassHandler<MyListItem>((c, _) => c._ApplyLayout());
    }

    public MyListItem()
    {
        PointerPressed += _OnPointerPressed;
        PointerReleased += _OnPointerReleased;
        PointerExited += _OnPointerExited;
        SizeChanged += (_, _) => _ApplyLayout();
        AttachedToVisualTree += (_, _) =>
        {
            // 主题资源此时必然就绪：补一次选中前景（构造期资源可能尚未注入）
            _RefreshCheckedForeground();
            if (Type == MyListItemCheckType.RadioBox) _EnsureSingleChecked();
        };
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _grid = e.NameScope.Find<Grid>("PART_Grid");
        _back = e.NameScope.Find<Border>("PART_Back");
        _checkBar = e.NameScope.Find<Border>("PART_Check");
        _logoHost = e.NameScope.Find<ContentPresenter>("PART_Logo");
        _buttonsHost = e.NameScope.Find<Panel>("PART_Buttons");
        _tagsHost = e.NameScope.Find<Panel>("PART_Tags");
        _title = e.NameScope.Find<TextBlock>("PART_Title");
        _info = e.NameScope.Find<TextBlock>("PART_Info");
        _UpdateLogo();
        _UpdateButtons();
        _UpdateTags();
        _RefreshInfoVisibility();
        _RefreshCheckedForeground();
        _ApplyLayout();
    }

    public override string ToString() => Title ?? "";

    #region 布局（列宽随类型/Logo/高度变化，WPF OnSizeChanged 同款）

    private bool _hasLogo;

    private void _ApplyLayout()
    {
        if (_grid is null || _grid.ColumnDefinitions.Count < 6) return;
        var compact = Height < 40d && Height > 0d;
        PseudoClasses.Set(":compact", compact);
        PseudoClasses.Set(":scaleAnim", IsScaleAnimationEnabled);

        // 背景浮层圆角（WPF RectBack：启用缩放动画或高 > 40 → 6，否则 0）
        if (_back is not null)
            _back.CornerRadius = new CornerRadius(IsScaleAnimationEnabled || !(Height < 40d && Height > 0d) ? 6 : 0);

        // 列 0：勾选条占位
        _grid.ColumnDefinitions[0].Width = new GridLength(
            Type is MyListItemCheckType.None or MyListItemCheckType.Clickable
                ? compact ? 4 : 2
                : 6);
        // 列 1：左缩进
        _grid.ColumnDefinitions[1].Width = new GridLength(PaddingLeft);
        // 列 2：Logo 区
        _hasLogo = !string.IsNullOrWhiteSpace(SvgIcon) || !string.IsNullOrWhiteSpace(Logo);
        _grid.ColumnDefinitions[2].Width = new GridLength((_hasLogo ? 34 : 0) + (compact ? 0 : 4));
        // 列 5：右缩进（悬停时被按钮扩展，见 _RefreshButtonsArea）
        _RefreshButtonsArea(IsPointerOver);
        // 标题上边距
        if (_title is not null)
            _title.Margin = new Thickness(4, 0, 0, compact ? 0 : 2);
        // Logo 内边距
        if (_logoControl is not null)
        {
            var isBitmap = Logo is { } logo &&
                           (logo.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ||
                            logo.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ||
                            logo.EndsWith(".webp", StringComparison.OrdinalIgnoreCase));
            _logoControl.Margin = isBitmap
                ? new Thickness(4, 5, 3, 5)
                : new Thickness(compact ? 6 : 8, 8, compact ? 4 : 6, 8);
        }
    }

    private void _RefreshButtonsArea(bool pointerOver)
    {
        if (_grid is null || _grid.ColumnDefinitions.Count < 6) return;
        var buttonCount = Buttons?.Count ?? 0;
        var right = buttonCount > 0 && pointerOver
            ? Math.Max(MinPaddingRight, 5 + buttonCount * 25)
            : MinPaddingRight;
        _grid.ColumnDefinitions[5].Width = new GridLength(right);
    }

    #endregion

    #region Logo

    private void _UpdateLogo()
    {
        if (_logoHost is null) return;
        _logoControl = null;

        var svgIcon = SvgIcon;
        var logo = Logo;
        if (!string.IsNullOrWhiteSpace(svgIcon))
        {
            var icon = new SvgIconControl { Icon = svgIcon };
            icon.Bind(SvgIconControl.IconBrushProperty, this.GetObservable(ForegroundProperty));
            _logoControl = icon;
        }
        else if (!string.IsNullOrWhiteSpace(logo))
        {
            if (logo.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            {
                // 网络图片：缓存 + 异步加载
                var image = new Image { Stretch = Stretch.Uniform };
                if (ImageCache.TryGetValue(logo, out var cached))
                    image.Source = cached;
                else
                    _ = _LoadRemoteImageAsync(logo, image);
                _logoControl = image;
            }
            else if (logo.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ||
                     logo.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ||
                     logo.EndsWith(".webp", StringComparison.OrdinalIgnoreCase))
            {
                // 本地位图
                try
                {
                    using var stream = File.OpenRead(logo);
                    var image = new Image
                    {
                        Source = new Bitmap(stream),
                        Stretch = Stretch.Uniform
                    };
                    _logoControl = image;
                }
                catch
                {
                    // 文件缺失：不渲染
                }
            }
            else
            {
                // 矢量 Path（前景色填充）
                try
                {
                    var path = new PathShape
                    {
                        Data = PathGeometry.Parse(logo),
                        Stretch = Stretch.Uniform,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center
                    };
                    path.Bind(PathShape.FillProperty, this.GetObservable(ForegroundProperty));
                    _logoControl = path;
                }
                catch
                {
                    // 无法解析：不渲染
                }
            }
        }

        if (_logoControl is not null)
        {
            var scale = string.IsNullOrWhiteSpace(svgIcon) ? LogoScale : 1d;
            if (!scale.Equals(1d))
            {
                _logoControl.RenderTransform = new ScaleTransform(scale, scale);
                _logoControl.RenderTransformOrigin = new RelativePoint(0.5, 0.5, RelativeUnit.Relative);
            }
            _logoHost.Content = _logoControl;
            _logoHost.IsVisible = true;

            if (LogoClickable)
            {
                _logoControl.IsHitTestVisible = true;
                _logoControl.PointerPressed += (_, _) => _logoDown = true;
                _logoControl.PointerExited += (_, _) => _logoDown = false;
                _logoControl.PointerReleased += (s, e) =>
                {
                    if (!_logoDown) return;
                    _logoDown = false;
                    LogoClick?.Invoke(this, e);
                };
            }
        }
        else
        {
            _logoHost.Content = null;
            _logoHost.IsVisible = false;
        }

        _ApplyLayout();
    }

    private static async Task _LoadRemoteImageAsync(string url, Image image)
    {
        try
        {
            var bytes = await Http.GetByteArrayAsync(url);
            var bitmap = await Task.Run(() =>
            {
                using var stream = new MemoryStream(bytes);
                return new Bitmap(stream);
            });
            ImageCache[url] = bitmap;
            Dispatcher.UIThread.Post(() => image.Source = bitmap);
        }
        catch
        {
            // 网络失败：保持空
        }
    }

    #endregion

    #region 右侧按钮 / 标签

    private void _UpdateButtons()
    {
        if (_buttonsHost is null) return;
        _buttonsHost.Children.Clear();
        if (Buttons is null || Buttons.Count == 0) return;

        foreach (var button in Buttons)
        {
            if (double.IsNaN(button.Width)) button.Width = 25d;
            if (double.IsNaN(button.Height)) button.Height = 25d;
            button.Margin = new Thickness(0, 0, 5, 0);
        }

        Panel host = Buttons.Count == 1
            ? new Panel()
            : new StackPanel { Orientation = Orientation.Horizontal };
        foreach (var button in Buttons) host.Children.Add(button);
        host.HorizontalAlignment = HorizontalAlignment.Right;
        host.VerticalAlignment = VerticalAlignment.Center;
        host.Margin = new Thickness(0, 0, 5, 0);

        _buttonsHost.Children.Add(host);
        _RefreshButtonsArea(IsPointerOver);
    }

    private void _RefreshInfoVisibility()
    {
        if (_info is not null)
            _info.IsVisible = !string.IsNullOrEmpty(Info);
    }

    private void _UpdateTags()
    {
        if (_tagsHost is null) return;
        _tagsHost.Children.Clear();

        IEnumerable<string>? keys = null;
        if (Tags is string str) keys = str.Split('|');
        else if (Tags is IEnumerable<string> list) keys = list;
        if (keys is not null)
        {
            foreach (var key in keys.Where(k => !string.IsNullOrWhiteSpace(k)).Distinct())
            {
                var text = global::PCL.Core.App.Localization.Lang.Text(key);
                if (!string.IsNullOrEmpty(text)) _AddTagBubble(text);
            }
        }
        else if (PlainTags is not null)
        {
            foreach (var text in PlainTags.Where(t => !string.IsNullOrWhiteSpace(t)))
                _AddTagBubble(text);
        }
    }

    private void _AddTagBubble(string text)
    {
        if (_tagsHost is null) return;
        _tagsHost.Children.Add(new Border
        {
            Background = new ImmutableSolidColorBrush(Color.FromArgb(17, 0, 0, 0)),
            Padding = new Thickness(3, 1),
            CornerRadius = new CornerRadius(3),
            Margin = new Thickness(0, 0, 4, 0),
            Child = new TextBlock
            {
                Text = text,
                FontSize = 11,
                Foreground = new ImmutableSolidColorBrush(Color.FromRgb(134, 134, 134))
            }
        });
        _tagsHost.IsVisible = _tagsHost.Children.Count > 0;
    }

    #endregion

    #region 勾选状态

    /// <summary>手动设置勾选状态（对齐 WPF SetChecked：事件可取消、单选互斥）。</summary>
    public void SetChecked(bool value, bool user, bool anime = true)
    {
        try
        {
            var args = new RouteEventArgs(user);
            var rawValue = Checked;
            _isUserInteraction = user;

            if (Type == MyListItemCheckType.RadioBox)
            {
                if (value != Checked)
                {
                    _SetCheckedValue(value);
                    Changed?.Invoke(this, args);
                    if (args.Handled)
                    {
                        _SetCheckedValue(rawValue);
                        return;
                    }
                }

                _SetCheckedValue(value);
            }
            else
            {
                if (value == Checked) return;
                _SetCheckedValue(value);
                Changed?.Invoke(this, args);
                if (args.Handled)
                {
                    _SetCheckedValue(rawValue);
                    return;
                }
            }

            if (value)
            {
                var checkArgs = new RouteEventArgs(user);
                Check?.Invoke(this, checkArgs);
                if (checkArgs.Handled) return;
            }

            if (Type == MyListItemCheckType.RadioBox && Parent is Panel) _EnsureSingleChecked();

            if (_isUserInteraction && Checked != rawValue) _ExecuteCheckCommand();
        }
        finally
        {
            _isUserInteraction = false;
        }
    }

    private void _SetCheckedValue(bool value)
    {
        _isSettingChecked = true;
        try
        {
            SetValue(CheckedProperty, value);
            PseudoClasses.Set(":checked", value);
            _RefreshCheckedForeground();
        }
        finally
        {
            _isSettingChecked = false;
        }
    }

    /// <summary>选中前景（WPF RefreshCheckedVisual 同款：ColorBrush2 / 紧凑 ColorBrush3 / 未选 ColorBrush1）。</summary>
    private void _RefreshCheckedForeground()
    {
        var key = Checked
            ? Height is > 0d and < 40d ? "ColorBrush3" : "ColorBrush2"
            : "ColorBrush1";
        // 同步解析资源（构造期 DynamicResource 在主题资源注入前解析会失败，故此处直接取值；
        // AttachedToVisualTree 时资源必然就绪，会再刷新一次）
        var app = Application.Current;
        if (app?.Resources.TryGetResource(key, app.ActualThemeVariant, out var value) != true) return;
        Foreground = value switch
        {
            IBrush brush => brush,
            Color color => new ImmutableSolidColorBrush(color),
            _ => Foreground
        };
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == CheckedProperty && !_isSettingChecked)
        {
            PseudoClasses.Set(":checked", Checked);
            _RefreshCheckedForeground();
            _EnsureSingleChecked();
        }
    }

    /// <summary>RadioBox 互斥：父容器内 0 个选中时自动选第一个，多于 1 个时仅保留一个。</summary>
    private void _EnsureSingleChecked()
    {
        if (Type != MyListItemCheckType.RadioBox || Parent is not Panel parent) return;
        var siblings = parent.Children.OfType<MyListItem>()
            .Where(item => item.Type == MyListItemCheckType.RadioBox)
            .ToList();
        if (siblings.Count == 0) return;

        var checkedItems = siblings.Where(item => item.Checked).ToList();
        switch (checkedItems.Count)
        {
            case 0:
                siblings[0].Checked = true;
                break;
            case > 1:
                if (Checked)
                    foreach (var item in checkedItems.Where(item => !ReferenceEquals(item, this)))
                        item.Checked = false;
                else
                    foreach (var item in checkedItems.Skip(1))
                        item.Checked = false;
                break;
        }
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

    #endregion

    #region 点击

    private void _OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed &&
            Type != MyListItemCheckType.None)
        {
            _pressed = true;
            PseudoClasses.Set(":pressed", true);
        }
    }

    private void _OnPointerExited(object? sender, PointerEventArgs e)
    {
        _pressed = false;
        _logoDown = false;
        PseudoClasses.Set(":pressed", false);
        _RefreshButtonsArea(false);
    }

    protected override void OnPointerEntered(PointerEventArgs e)
    {
        base.OnPointerEntered(e);
        _RefreshButtonsArea(true);
    }

    private void _OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (!_pressed) return;
        _pressed = false;
        PseudoClasses.Set(":pressed", false);
        if (!Bounds.Contains(e.GetPosition(this))) return;

        Click?.Invoke(this, e);
        _ExecuteCommand();

        switch (Type)
        {
            case MyListItemCheckType.RadioBox:
                if (!Checked) SetChecked(true, true, true);
                break;
            case MyListItemCheckType.CheckBox:
                SetChecked(!Checked, true, true);
                break;
        }
    }

    private void _ExecuteCommand()
    {
        var command = Command;
        if (_isCommandRunning || command is null || command.CanExecute(CommandParameter) != true) return;
        _isCommandRunning = true;
        try
        {
            command.Execute(CommandParameter);
        }
        finally
        {
            _isCommandRunning = false;
        }
    }

    #endregion
}
