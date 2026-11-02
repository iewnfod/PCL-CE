using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Interactivity;
using Avalonia.Media;

namespace PCL.Avalonia.Controls;

/// <summary>
///     PCL 卡片容器：白色圆角卡片 + 可选标题行与折叠。<br/>
///     对应 WPF 版 <c>MyCard</c> 的常用子集（Title / CanToggle / IsExpanded）。
/// </summary>
[TemplatePart("PART_Expander", typeof(ToggleButton))]
public class MyCard : ContentControl
{
    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<MyCard, string?>(nameof(Title));

    public static readonly StyledProperty<bool> CanToggleProperty =
        AvaloniaProperty.Register<MyCard, bool>(nameof(CanToggle));

    public static readonly StyledProperty<bool> IsExpandedProperty =
        AvaloniaProperty.Register<MyCard, bool>(nameof(IsExpanded), true);

    public static readonly StyledProperty<object?> TitleRightProperty =
        AvaloniaProperty.Register<MyCard, object?>(nameof(TitleRight));

    public string? Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    /// <summary>是否允许点击标题栏折叠/展开。</summary>
    public bool CanToggle
    {
        get => GetValue(CanToggleProperty);
        set => SetValue(CanToggleProperty, value);
    }

    /// <summary>当前是否展开（CanToggle 时有效）。</summary>
    public bool IsExpanded
    {
        get => GetValue(IsExpandedProperty);
        set => SetValue(IsExpandedProperty, value);
    }

    /// <summary>标题行右侧的附加内容（如按钮）。</summary>
    public object? TitleRight
    {
        get => GetValue(TitleRightProperty);
        set => SetValue(TitleRightProperty, value);
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (e.NameScope.Find("PART_Expander") is ToggleButton toggle)
            toggle.Click += (_, _) =>
            {
                if (CanToggle) IsExpanded = !IsExpanded;
            };
    }
}
