using System;
using System.IO;

namespace PCL.Core.Utils.OS;

public static class SystemPaths {
    /// <summary>
    /// 系统盘符（Windows 下含冒号和反斜杠，例如 "C:\"；Unix 下为根目录 "/"，仅用于兜底拼接）。
    /// </summary>
    public static string DriveLetter { get; } =
        OperatingSystem.IsWindows()
            ? Path.GetPathRoot(Environment.SystemDirectory) ?? "C:\\"
            : "/";
}
