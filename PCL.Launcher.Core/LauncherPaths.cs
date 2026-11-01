using PCL.Core.App;

namespace PCL;

/// <summary>业务层使用的路径常量。</summary>
public static class LauncherPaths
{
    /// <summary>Mesa Loader Windows 版本。</summary>
    public const string MesaLoaderWindowsVersion = "26.0.4";

    /// <summary>程序内嵌图片根路径。</summary>
    public const string ImageRoot =
        "pack://application:,,,/Plain Craft Launcher 2;component/Images/";

    private static readonly char DirSep = System.IO.Path.DirectorySeparatorChar;

    /// <summary>程序可执行文件所在目录，以目录分隔符结尾。</summary>
    public static string ExecutableDirectory { get; } =
        EnsureTrailingSeparator(Basics.ExecutableDirectory, DirSep);

    /// <summary>临时目录，以目录分隔符结尾。</summary>
    public static string Temp => EnsureTrailingSeparator(Paths.Temp, DirSep);

    /// <summary>可用于临时存放文件的、不含特殊字符的目录，以目录分隔符结尾。</summary>
    public static string PureAsciiDirectory
    {
        get
        {
            if (OperatingSystem.IsWindows())
            {
                if (ExecutableDirectory.All(c => c < 128))
                    return EnsureTrailingSeparator(System.IO.Path.Combine(ExecutableDirectory, "PCL"), DirSep);
                var temp = System.IO.Path.GetTempPath();
                if (temp.All(c => c < 128))
                    return EnsureTrailingSeparator(temp, DirSep);
                return System.IO.Path.Combine(
                    System.IO.Path.GetPathRoot(Environment.SystemDirectory) ?? "C:\\",
                    "ProgramData",
                    "PCL");
            }

            // Unix：文件系统与 Java 均以 UTF-8 处理路径，无 8.3/GBK 短路径问题，
            // 临时性辅助文件（JavaWrapper/authlib-injector 等）直接放系统临时目录。
            return EnsureTrailingSeparator(System.IO.Path.GetTempPath(), DirSep);
        }
    }

    /// <summary>当前 Minecraft 文件夹路径，由 UI 宿主同步设置。</summary>
    public static string? MinecraftFolder { get; set; }

    private static string EnsureTrailingSeparator(string path, char separator) =>
        string.IsNullOrEmpty(path) || path.EndsWith(separator)
            ? path
            : path + separator;
}
