using System;

namespace PCL.Core.UI.Theme;

public static class SystemThemeHelper {
    /// <summary>
    /// 检查系统是否处于深色模式（委托唯一实现 <see cref="Utils.OS.SystemTheme" />，
    /// 内部按平台读取注册表 / gsettings）。
    /// </summary>
    /// <returns>如果系统使用深色模式，则返回 true；否则返回 false（包括注册表不可访问的情况）。</returns>
    public static bool IsSystemInDarkMode() => Utils.OS.SystemTheme.IsSystemInDarkMode();

    [Obsolete("Use ThemeService.IsDarkMode instead")]
    public static bool IsDarkMode() => ThemeService.IsDarkMode;
}
