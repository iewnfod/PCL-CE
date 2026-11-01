using System.Xml.Linq;
using Avalonia.Threading;
using PCL.Core.App.Localization;
using PCL.Core.Platform;

namespace PCL.Avalonia.Platform;

/// <summary>
///     基于 XML 解析的本地化后端：直接读取 PCL.Core.Wpf 的 WPF 语言资源字典
///     （<c>sys:String x:Key</c> 条目），提取字符串键值供 Avalonia 侧使用，
///     无需 WPF XamlReader。
/// </summary>
public sealed class AvaloniaLocalizationBackend : ILocalizationBackend
{
    private const string ResourcePrefix = "PCL.Avalonia.Languages.";

    private static readonly Dictionary<string, string> BaseStrings = [];
    private static readonly Dictionary<string, string> CurrentStrings = [];

    public bool CheckAccess() => Dispatcher.UIThread.CheckAccess();

    public void Invoke(Action action) => Dispatcher.UIThread.Post(action);

    public string? TryFindResource(string key)
    {
        return CurrentStrings.GetValueOrDefault(key) ?? BaseStrings.GetValueOrDefault(key);
    }

    public void ApplyLanguageResources(string baseLanguageCode, string? currentLanguageCode)
    {
        BaseStrings.Clear();
        foreach (var (key, value) in _LoadLanguage(baseLanguageCode))
            BaseStrings[key] = value;

        CurrentStrings.Clear();
        if (currentLanguageCode is not null)
            foreach (var (key, value) in _LoadLanguage(currentLanguageCode))
                CurrentStrings[key] = value;
    }

    public void ApplyLaunchFont(string? customFontName, LocalizationLanguage? language)
    {
        // MVP：Avalonia 使用系统默认字体栈；中文字体策略在控件库阶段细化
    }

    private static IEnumerable<(string Key, string Value)> _LoadLanguage(string languageCode)
    {
        var assembly = typeof(AvaloniaLocalizationBackend).Assembly;
        var stream = assembly.GetManifestResourceStream($"{ResourcePrefix}{languageCode}.xaml");
        if (stream is null)
        {
            Console.WriteLine($"[Localization] 语言资源缺失：{languageCode}");
            yield break;
        }
        using (stream)
        {
            var document = XDocument.Load(stream);
            var xKey = XName.Get("Key", "http://schemas.microsoft.com/winfx/2006/xaml");
            foreach (var element in document.Descendants())
            {
                var key = element.Attribute(xKey)?.Value;
                if (key is null) continue;
                // 只提取字符串条目（sys:String）；{0} 占位符原样保留
                var text = element.Value;
                if (!string.IsNullOrEmpty(text))
                    yield return (key, text);
            }
        }
    }
}
