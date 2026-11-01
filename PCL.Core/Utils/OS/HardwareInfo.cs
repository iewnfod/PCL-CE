using System;
using System.Collections.Generic;
using System.IO;
using PCL.Core.Logging;

namespace PCL.Core.Utils.OS;

public static class HardwareInfo
{
    private static readonly object _Lock = new();

    /// <summary>
    /// 系统 CPU 信息
    /// </summary>
    public static string CPUName = "Unknown";

    /// <summary>
    /// 系统 GPU 信息
    /// </summary>
    public static IReadOnlyList<GPUInfo> GPUs { get; private set; } = [];

    private static long _SystemMemorySize;

    /// <summary>
    /// 已安装物理内存大小，单位 MiB（首次访问时初始化，避免静态构造器在非 Windows 平台触发 kernel32 调用）
    /// </summary>
    public static long SystemMemorySize
    {
        get
        {
            if (_SystemMemorySize == 0) _SystemMemorySize = _GetPhysicalMemoryMiB();
            return _SystemMemorySize;
        }
    }

    public readonly record struct GPUInfo(string Name, string DriverVersion, long Memory);

    /// <summary>
    /// 获取系统信息，例如 CPU 与 GPU，并存储到 CPUName 和 GPUs
    /// </summary>
    public static void GetHardwareInfo()
    {
#if WINDOWS
        GetHardwareInfoWindows();
#else
        GetHardwareInfoUnix();
#endif
        LogWrapper.Info("已获取系统硬件信息");
    }

#if WINDOWS
    private static void GetHardwareInfoWindows()
    {
        // CPU
        var cpuName = (string?)null;
        try
        {
            using var searcher = new System.Management.ManagementObjectSearcher(@"root\CIMV2", "SELECT * FROM Win32_Processor");
            foreach (System.Management.ManagementObject queryObj in searcher.Get())
            {
                cpuName = queryObj["Name"]?.ToString()?.Trim();
                break;
            }
        }
        catch (Exception ex)
        {
            LogWrapper.Warn(ex, "获取 CPU 信息时出错");
        }

        // GPU
        var gpuList = new List<GPUInfo>();
        try
        {
            using var searcher =
                new System.Management.ManagementObjectSearcher(@"root\CIMV2", "SELECT * FROM Win32_VideoController");
            foreach (System.Management.ManagementObject queryObj in searcher.Get())
            {
                var gpuInfo = new GPUInfo
                {
                    Name = queryObj["Name"]?.ToString() ?? "",
                    DriverVersion = queryObj["DriverVersion"]?.ToString() ?? "",
                    Memory = queryObj["AdapterRAM"] is not null and not System.DBNull
                        ? Convert.ToInt64(queryObj["AdapterRAM"]) / (1024 * 1024)
                        : 0
                };
                gpuList.Add(gpuInfo);
            }
        }
        catch (Exception ex)
        {
            LogWrapper.Warn(ex, "获取 GPU 信息时出错");
        }

        lock (_Lock)
        {
            if (cpuName is not null)
                CPUName = cpuName;
            if (gpuList.Count > 0)
                GPUs = gpuList;
        }
    }

    private static long _GetPhysicalMemoryMiB() =>
        (long)KernelInterop.GetPhysicalMemoryBytes().Total / 1024 / 1024;
#else
    private static void GetHardwareInfoUnix()
    {
        // CPU：/proc/cpuinfo 的 model name
        var cpuName = (string?)null;
        try
        {
            foreach (var line in File.ReadLines("/proc/cpuinfo"))
            {
                if (line.StartsWith("model name", StringComparison.Ordinal) &&
                    line.AsSpan().TrimStart().ToString().Split(':', 2) is { Length: 2 } parts)
                {
                    cpuName = parts[1].Trim();
                    break;
                }
            }
        }
        catch (Exception ex)
        {
            LogWrapper.Warn(ex, "获取 CPU 信息时出错");
        }

        // GPU：读取 /sys/class/drm/card*/device/vendor 之外的通用信息在用户态不可靠，留空待 lspci 增强
        var gpuList = new List<GPUInfo>();

        lock (_Lock)
        {
            if (cpuName is not null)
                CPUName = cpuName;
            if (gpuList.Count > 0)
                GPUs = gpuList;
        }
    }

    private static long _GetPhysicalMemoryMiB()
    {
        try
        {
            foreach (var line in File.ReadLines("/proc/meminfo"))
            {
                if (line.StartsWith("MemTotal:", StringComparison.Ordinal))
                {
                    // 格式：MemTotal:  16384256 kB
                    var span = line.AsSpan("MemTotal:".Length).Trim();
                    if (span.EndsWith("kB", StringComparison.Ordinal)) span = span[..^2];
                    if (long.TryParse(span, out var kb))
                        return kb / 1024;
                }
            }
        }
        catch (Exception ex)
        {
            LogWrapper.Warn(ex, "读取 /proc/meminfo 失败");
        }
        // 兜底：没有物理内存数据时用 GC 感知的内存近似，避免 0 导致上游除零
        return Math.Max(GC.GetGCMemoryInfo().TotalAvailableMemoryBytes / 1024 / 1024, 1024);
    }
#endif
}
