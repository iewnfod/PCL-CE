using PCL.Core.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace PCL.Core.Minecraft.Java.Scanner;

/// <summary>
///     Unix Java 扫描器：枚举主流发行版与工具链的 Java 安装位置。<br/>
///     覆盖：/usr/lib/jvm（Debian/Arch/Fedora 标准）、/opt（手动解压）、
///     ~/.sdkman、~/.jdks（IntelliJ）、~/.minecraft/runtime（官方启动器），
///     以及 PATH 上的 java（含 alternatives 软链解析）。
/// </summary>
public class UnixJavaScanner : IJavaScanner
{
    public void Scan(ICollection<string> results)
    {
        if (OperatingSystem.IsWindows()) return;

        try
        {
            var roots = new List<(string Path, int Depth)>();

            // 1. PATH 上的 java（含 readlink 解析软链到真实 JDK）
            var pathJava = _ResolveWhichJava();
            if (pathJava is not null) results.Add(pathJava);

            // 2. 发行版标准位置：/usr/lib/jvm/<distro-jdk>/bin/java
            roots.Add(("/usr/lib/jvm", 2));
            roots.Add(("/opt/java", 3));
            roots.Add(("/opt", 1)); // /opt/jdk-21、/opt/zulu21 等
            // 3. 用户目录工具链
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (!string.IsNullOrEmpty(home))
            {
                roots.Add((Path.Combine(home, ".sdkman/candidates/java"), 2));
                roots.Add((Path.Combine(home, ".jdks"), 2));
                roots.Add((Path.Combine(home, ".minecraft/runtime"), 5));
                roots.Add((Path.Combine(home, "opt"), 1));
            }

            foreach (var (root, depth) in roots)
                _ScanDirectory(root, depth, results);
        }
        catch (Exception ex)
        {
            LogWrapper.Error(ex, "Java", "Unix 路径扫描失败");
        }
    }

    /// <summary>which java + readlink -f 解析软链（Debian alternatives 等）。</summary>
    private static string? _ResolveWhichJava()
    {
        try
        {
            var psi = new ProcessStartInfo("/bin/sh", "-c \"command -v java\"")
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            };
            using var process = Process.Start(psi);
            if (process is null) return null;
            var which = process.StandardOutput.ReadLine()?.Trim();
            process.WaitForExit(5000);
            if (string.IsNullOrEmpty(which) || !File.Exists(which)) return null;

            // 软链解析：alternatives 的 /usr/bin/java → /usr/lib/jvm/.../bin/java
            try
            {
                var resolved = Path.GetFullPath(which);
                var linkTarget = resolved;
                while (File.Exists(linkTarget))
                {
                    var target = File.ResolveLinkTarget(linkTarget, true);
                    if (target is null) break;
                    linkTarget = target.FullName;
                }
                return File.Exists(linkTarget) ? linkTarget : which;
            }
            catch
            {
                return which;
            }
        }
        catch (Exception ex)
        {
            LogWrapper.Debug($"command -v java 失败: {ex.Message}");
            return null;
        }
    }

    /// <summary>在 root 下（含子目录至 depth 层）查找 bin/java。</summary>
    private static void _ScanDirectory(string root, int maxDepth, ICollection<string> results)
    {
        if (!Directory.Exists(root)) return;

        var queue = new Queue<(string Path, int Depth)>();
        queue.Enqueue((root, 0));

        while (queue.Count > 0)
        {
            var (current, depth) = queue.Dequeue();
            if (depth > maxDepth) continue;

            // 当前目录即含 bin/java
            var javaBin = Path.Combine(current, "bin", JavaConsts.JavaExecutableName);
            if (File.Exists(javaBin))
            {
                results.Add(javaBin);
                continue; // 已是 JDK 根，不再下钻
            }

            try
            {
                foreach (var dir in Directory.EnumerateDirectories(current))
                    queue.Enqueue((dir, depth + 1));
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or DirectoryNotFoundException)
            {
                // 无权限/瞬时故障目录跳过
            }
        }
    }
}
