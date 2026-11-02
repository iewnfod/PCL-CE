using Avalonia;
using Avalonia.Markup.Xaml.MarkupExtensions;

namespace PCL.Avalonia.Controls;

/// <summary>code-behind 动态资源帮助（等价 WPF SetResourceReference）。</summary>
public static class UiResources
{
    /// <summary>把属性绑定到应用资源键（主题切换时自动更新）。</summary>
    public static void SetDynamic(this AvaloniaObject obj, AvaloniaProperty property, string key)
        => obj.Bind(property, new DynamicResourceExtension(key));
}
