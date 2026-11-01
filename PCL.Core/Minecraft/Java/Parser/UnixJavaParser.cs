using PCL.Core.Logging;
using PCL.Core.Utils;
using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace PCL.Core.Minecraft.Java.Parser;

/// <summary>
///     Unix Java 解析器：通过执行 <c>java -version</c>（输出到 stderr）解析版本与品牌，
///     替代 Windows 的 PE 头 / FileVersionInfo 方案。
/// </summary>
public partial class UnixJavaParser : IJavaParser
{
    [GeneratedRegex(@"version\s+""(?<version>[0-9]+(?:\.[0-9]+)*(?:_[0-9]+)?)""")]
    private static partial Regex _VersionRegex();

    public JavaInstallation? Parse(string javaExePath)
    {
        if (!File.Exists(javaExePath)) return null;

        LogWrapper.Info("Java", $"解析 {javaExePath} 的 Java 程序信息（java -version）");

        var (output, exitCode) = _RunJavaVersion(javaExePath);
        if (exitCode != 0 || string.IsNullOrEmpty(output))
        {
            LogWrapper.Warn("Java", $"无法执行 {javaExePath} -version（exit={exitCode}）");
            return null;
        }

        // 版本：java version "21.0.2" / openjdk version "21.0.11" 2026-04-15
        var versionMatch = _VersionRegex().Match(output);
        if (!versionMatch.Success)
        {
            LogWrapper.Warn("Java", $"无法从输出解析版本号：{output}");
            return null;
        }

        // 归一化版本号（保证至少 Major.Minor.Build，且处理 1.8.0_392 → 1.8.0）
        var versionString = versionMatch.Groups["version"].Value;
        var version = _NormalizeVersion(versionString);

        var brand = _DetermineBrand(output);
        var is64Bit = output.Contains("64-Bit", StringComparison.OrdinalIgnoreCase) ||
                      output.Contains("64-bit", StringComparison.OrdinalIgnoreCase) ||
                      RuntimeInformation.ProcessArchitecture == Architecture.X64 ||
                      RuntimeInformation.ProcessArchitecture == Architecture.Arm64;
        var arch = _MapArchitecture(RuntimeInformation.ProcessArchitecture);

        var javaFolder = Path.GetDirectoryName(Path.GetFullPath(javaExePath))!;
        var isJre = !File.Exists(Path.Combine(javaFolder, "javac"));

        return new JavaInstallation(javaFolder, version, brand, arch, is64Bit, isJre);
    }

    private static (string Output, int ExitCode) _RunJavaVersion(string javaExePath)
    {
        try
        {
            var psi = new ProcessStartInfo(javaExePath, "-version")
            {
                UseShellExecute = false,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            };
            using var process = Process.Start(psi);
            if (process is null) return (string.Empty, -1);
            // -version 输出在 stderr（Oracle 惯例），stdout 兜底拼接
            var output = process.StandardError.ReadToEnd() + process.StandardOutput.ReadToEnd();
            process.WaitForExit(10_000);
            return (output, process.HasExited ? process.ExitCode : -1);
        }
        catch (Exception ex)
        {
            LogWrapper.Warn(ex, "Java", $"执行 {javaExePath} -version 失败");
            return (string.Empty, -1);
        }
    }

    private static Version _NormalizeVersion(string versionString)
    {
        // 去掉 8u392 / 21.0.11.0.11+10-GA 之类的后缀
        var plus = versionString.IndexOf('+');
        if (plus > 0) versionString = versionString[..plus];
        var underscore = versionString.IndexOf('_');
        if (underscore > 0) versionString = versionString[..underscore];

        // System.Version 最多 4 段；发行版构建号常为 5 段（如 21.0.11.0.11），截断到 4 段
        var parts = versionString.Split('.');
        if (parts.Length > 4) versionString = string.Join(".", parts[..4]);
        // 单段（"21"）→ 21.0
        if (parts.Length == 1) versionString += ".0";
        return Version.TryParse(versionString, out var v) ? v : new Version(0, 0, 0);
    }

    private static JavaBrandType _DetermineBrand(string output)
    {
        foreach (var (keyword, brand) in _BrandMap)
        {
            if (output.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                return brand;
        }
        return JavaBrandType.OpenJDK;
    }

    private static MachineType _MapArchitecture(Architecture architecture) => architecture switch
    {
        Architecture.X64 => MachineType.AMD64,
        Architecture.X86 => MachineType.I386,
        Architecture.Arm64 => MachineType.ARM64,
        Architecture.Arm => MachineType.ARM,
        _ => MachineType.Unknown
    };

    private static readonly (string Keyword, JavaBrandType Brand)[] _BrandMap =
    [
        ("Dragonwell", JavaBrandType.Dragonwell),
        ("Alibaba", JavaBrandType.Dragonwell),
        ("Temurin", JavaBrandType.EclipseTemurin),
        ("Eclipse", JavaBrandType.EclipseTemurin),
        ("Liberica", JavaBrandType.Liberica),
        ("Bellsoft", JavaBrandType.Liberica),
        ("Corretto", JavaBrandType.Corretto),
        ("Amazon", JavaBrandType.Corretto),
        ("Zulu", JavaBrandType.Zulu),
        ("Azul", JavaBrandType.Zulu),
        ("Semeru", JavaBrandType.IBMSemeru),
        ("IBM", JavaBrandType.IBMSemeru),
        ("Kona", JavaBrandType.TencentKona),
        ("Tencent", JavaBrandType.TencentKona),
        ("GraalVM", JavaBrandType.GraalVmCommunity),
        ("JetBrains", JavaBrandType.JetBrains),
        ("Microsoft", JavaBrandType.Microsoft),
        ("Oracle", JavaBrandType.Oracle),
        ("OpenJDK", JavaBrandType.OpenJDK)
    ];
}
