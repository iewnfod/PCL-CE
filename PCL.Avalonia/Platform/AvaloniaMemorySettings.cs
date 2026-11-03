using PCL.Core.App;
using PCL.Core.Utils.OS;

namespace PCL.Avalonia.Platform;

/// <summary>
///     内存分配策略（1:1 复刻 WPF PageSetupLaunch/PageInstanceSetupViewModel 内存计算）：
///     实例级 MemorySolution（2=使用全局 / 0=动态 / 1=自定义）→ 全局 MemoryAllocationMode（0=自动 / 1=自定义刻度），
///     32 位 Java 上限 1G；初始堆（-Xms）遵循「锁定内存 / 自定义 -Xms / 仅手动模式」语义。
/// </summary>
internal sealed class AvaloniaMemorySettings : IMemorySettings
{
    /// <summary>内存滑块刻度值 → GB（与 WPF PageSetupLaunch.GetRamFromTick 一致）。</summary>
    public static double GetRamFromTick(int value)
    {
        return value switch
        {
            <= 12 => value * 0.1d + 0.3d,
            <= 25 => (value - 12) * 0.5d + 1.5d,
            <= 33 => (value - 25) * 1 + 8,
            _ => (value - 33) * 2 + 16,
        };
    }

    /// <summary>GB → 不超过该值的最小刻度值（滑块初始化用）。</summary>
    public static int GetTickFromRam(double gb)
    {
        for (var tick = 49; tick >= 0; tick--)
            if (GetRamFromTick(tick) <= gb)
                return tick;
        return 0;
    }

    /// <summary>生效的自定义 JVM 参数中是否已包含 -Xms（实例级参数非空时完全忽略全局参数）。</summary>
    private static bool HasCustomXms(McInstance? version)
    {
        var instanceArgs = version?.PathInstance is { } path ? Config.Instance.JvmArgs[path] : "";
        var effectiveArgs = string.IsNullOrEmpty(instanceArgs) ? Config.Launch.JvmArgs : instanceArgs;
        return !string.IsNullOrEmpty(effectiveArgs) &&
            effectiveArgs.Contains("-Xms", StringComparison.OrdinalIgnoreCase);
    }

    public double GetRam(McInstance? version, bool? is32BitJava = null)
    {
        var instancePath = version?.PathInstance;
        // 实例级配置仅在存在具体实例时读取（DynamicCacheConfigStorage 要求非空上下文）；无实例时走全局
        var solution = instancePath is null ? 2 : Config.Instance.MemorySolution[instancePath];
        // 实例级：0=动态 / 1=自定义 / 2=使用全局（全局：MemoryAllocationMode 0=自动 1=自定义）
        var isDynamic = solution switch
        {
            0 => true,
            1 => false,
            _ => Config.Launch.MemoryAllocationMode == 0,
        };

        double ramGive;
        if (isDynamic)
        {
            // 动态分配：按 Mod 数与可用内存估算
            ramGive = _GetDynamicRam(version);
        }
        else if (solution == 1)
        {
            // 实例级自定义
            ramGive = GetRamFromTick(Config.Instance.CustomMemorySize[instancePath]);
        }
        else
        {
            // 全局自定义
            ramGive = GetRamFromTick(Config.Launch.CustomMemorySize);
        }

        // 32 位 Java 上限 1G
        if (is32BitJava ?? !JavaRuntime.IsGameSet64BitJava(version))
            ramGive = Math.Min(1d, ramGive);
        return ramGive;
    }

    public double? GetInitialRam(McInstance? version, bool? is32BitJava = null)
    {
        // 锁定内存时 -Xms 恒等于 -Xmx，由启动路径处理
        if (Config.Launch.LockMemory) return null;
        // 自定义 JVM 参数含 -Xms 时由其决定
        if (HasCustomXms(version)) return null;

        var instancePath = version?.PathInstance;
        switch (instancePath is null ? 2 : Config.Instance.MemorySolution[instancePath])
        {
            case 2:
                // 全局：仅手动分配模式下生效
                if (Config.Launch.MemoryAllocationMode != 1) return null;
                var globalTick = Config.Launch.CustomInitialMemorySize;
                if (globalTick <= 0) return null;
                return Math.Min(GetRamFromTick(globalTick), GetRam(version, is32BitJava));
            case 1:
                // 实例级自定义
                var instanceTick = Config.Instance.CustomInitialMemorySize[instancePath];
                if (instanceTick <= 0) return null;
                return Math.Min(GetRamFromTick(instanceTick), GetRam(version, is32BitJava));
            default:
                return null;
        }
    }

    /// <summary>动态内存估算（与 WPF 一致的四阶段目标曲线；Mod 数 / OptiFine 影响）。</summary>
    private static double _GetDynamicRam(McInstance? version)
    {
        var ramAvailable =
            Math.Round((double)(KernelInterop.GetAvailablePhysicalMemoryBytes() / 1024 / 1024 / 1024 * 10)) / 10;

        double ramMinimum, ramTarget1, ramTarget2, ramTarget3;
        if (version is not null && !version.IsLoaded)
            version.Load();
        if (version is not null && version.Modable)
        {
            var modDir = new DirectoryInfo(System.IO.Path.Combine(version.PathIndie, "mods"));
            var modCount = modDir.Exists ? modDir.GetFiles().Length : 0;
            ramMinimum = 0.5d + modCount / 150d;
            ramTarget1 = 1.5d + modCount / 90d;
            ramTarget2 = 2.7d + modCount / 50d;
            ramTarget3 = 4.5d + modCount / 25d;
        }
        else if (version is not null && version.Info.HasOptiFine)
        {
            ramMinimum = 0.5d;
            ramTarget1 = 1.5d;
            ramTarget2 = 3d;
            ramTarget3 = 5d;
        }
        else
        {
            ramMinimum = 0.5d;
            ramTarget1 = 1.5d;
            ramTarget2 = 2.5d;
            ramTarget3 = 4d;
        }

        var ramGive = 0d;
        var stages = new[]
        {
            (Delta: ramTarget1, Ratio: 1d),
            (Delta: ramTarget2 - ramTarget1, Ratio: 0.7d),
            (Delta: ramTarget3 - ramTarget2, Ratio: 0.4d),
            (Delta: ramTarget3, Ratio: 0.15d),
        };
        foreach (var (delta, ratio) in stages)
        {
            ramGive += Math.Min(ramAvailable * ratio, delta);
            ramAvailable -= delta / ratio;
            if (ramAvailable < 0.1d) break;
        }
        return Math.Round(Math.Max(ramGive, ramMinimum), 1);
    }
}
