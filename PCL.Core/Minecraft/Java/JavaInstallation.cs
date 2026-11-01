using PCL.Core.Utils;
using System;
using System.IO;

namespace PCL.Core.Minecraft.Java;

public sealed record JavaInstallation(
    string JavaFolder,
    Version Version,
    JavaBrandType Brand,
    MachineType Architecture,
    bool Is64Bit,
    bool IsJre)
{
    /// <summary>Java 可执行文件名：Windows 为 java.exe（启动用 javaw.exe），Unix 为 java。</summary>
    public static string JavaExecutableName { get; } = OperatingSystem.IsWindows() ? "java.exe" : "java";

    public string JavaExePath => Path.Combine(JavaFolder, JavaExecutableName);

    public string? JavawExePath
    {
        get
        {
            if (!OperatingSystem.IsWindows()) return null; // Unix 无 javaw，统一用 java
            var javaw = Path.Combine(JavaFolder, "javaw.exe");
            return File.Exists(javaw) ? javaw : null;
        }
    }

    /// <summary>
    /// Java 主版本号（处理 1.8 → 8 的映射）
    /// </summary>
    public int MajorVersion => Version.Major == 1 ? Version.Minor : Version.Major;

    /// <summary>
    /// 检查物理文件是否存在（合理查询，非状态存储）
    /// </summary>
    public bool IsStillAvailable => File.Exists(JavaExePath);

    public override string ToString() =>
        $"{(IsJre ? "JRE" : "JDK")} {MajorVersion} {Brand} {(Is64Bit ? "64 Bit" : "32 Bit")} | {JavaFolder}";

    public string ToDetailedString() =>
        $"{(IsJre ? "JRE" : "JDK")} {Version} {Brand} {(Is64Bit ? "64 Bit" : "32 Bit")} | {JavaFolder}";
}