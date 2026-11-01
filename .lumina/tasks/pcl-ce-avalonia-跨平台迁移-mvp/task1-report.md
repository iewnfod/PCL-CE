# 任务 1 报告：核心层多目标编译验证

## 结果总览

| 目标 | 结果 |
|---|---|
| PCL.Core / PCL.Launcher.Core / PCL.Core.Test × (net10.0 + net10.0-windows) | **全部 0 错误** |
| Plain Craft Launcher 2（WPF 主程序，Linux 交叉编译） | **0 错误，未被破坏** |
| Linux 测试基线（dotnet test -f net10.0） | **192 通过 / 13 失败 / 2 跳过**（起点 183/23/1） |

## 实施内容

### csproj 改造
- 三个项目多目标化：`net10.0;net10.0-windows`，windows 目标定义 `WINDOWS` 编译符号
- `System.Management`、`PlainToolkit.CngProtectedData` 仅在 windows 目标引用
- `Microsoft.Xaml.Behaviors.Wpf` 从 PCL.Core 移至真正使用它的 PCL.Core.Wpf
- 测试项目：windows 目标独占 `UseWPF` 与 `PCL.Core.Wpf` 引用；net10.0 排除 `BlurPerformanceTest`、`Validate/**`

### 代码平台守卫（#if WINDOWS / 运行时守卫）
| 文件 | 改动 |
|---|---|
| `Utils/OS/HardwareInfo.cs` | WMI→`/proc/cpuinfo`、`/proc/meminfo`；静态内存初始化改懒加载（原静态构造在 Linux 触发 kernel32 即炸） |
| `Utils/Secret/Identify.cs` | WMI 机器指纹→`/etc/machine-id` + DMI |
| `Utils/Secret/IdentifyOld.cs` | WMI CPU ID→`/proc/cpuinfo` Serial |
| `Utils/Secret/EncryptHelper.cs` | 新增 **v3 keyfile 密钥格式**（Unix，0600 权限）；v1/v2 DPAPI 仅 Windows |
| `Utils/OS/ProcessInterop.cs` | IsAdmin→geteuid；命令行→`/proc/<pid>/cmdline`；TASKKILL→Kill(entireProcessTree)；runas/GPU 注册表→no-op 守卫 |
| `IO/Net/Http/HttpProxyManager.cs` | **网络核心路径**：注册表系统代理→Unix 环境变量（http_proxy/https_proxy/all_proxy）；RegistryChangeMonitor 仅 Windows 实例化 |
| `UI/ToastNotification.cs` | WinRT Toast 非 Windows 静默跳过 |
| `App/AppEncodingInit.cs`（新增） | ModuleInitializer 注册 CodePagesEncodingProvider（GB2312/GB18030） |

### 测试修复
- LocalizationTest 语言文件路径：`PCL.Core/App/...`→`PCL.Core.Wpf/App/...`（i18n 重构后文件移动，测试未跟上——**既有 bug，Windows 上同样失败**）
- EncryptHelperKeyTest：v1→v2 迁移测试 Windows-only；重建版本断言按平台（2/3）

## 剩余 13 个 Linux 测试失败（全部归类）

| 类别 | 数量 | 性质 |
|---|---|---|
| Basics/ConfigStorage/CacheStore/EventBus（`PCL.metadata.json` 嵌入资源缺失 + 服务未初始化） | 9 | **既有问题**：测试宿主无主程序资源，Windows 上 dotnet test 同挂（CI 不跑测试） |
| zh-TW 翻译缺 97 键 | 2 | **既有问题**：上游 i18n 进行中 |
| EncodingDetector 将 gb2312 样本判为 UTF-8 | 1 | **真实跨平台差异**（Linux ICU vs Windows 编码表）→ 任务 2 |
| DoH SRV 查询 0 答案 | 1 | 网络/环境类 |

## 任务 2 清单（按优先级）

1. **运行时 P/Invoke 守卫**（编译过但 Linux 运行即炸）：`WatcherWindow`(user32)、`LauncherCommon.GetShortPathName`、`KernelInterop`、`NtInterop`、`WindowInterop`、`WinRTInterop`、`SystemTheme`(注册表)、`AumidHelper`、`RegionPolicy`(部分)、`StartupService`(自启)、`SingleInstanceService`(NamedPipe)
2. **Java 扫描**：新增 Unix 扫描器（which/update-alternatives/~/.sdkman/常见路径）
3. **路径卫生**：`LauncherPaths` 等处硬编码 `\`、`C:\`、`PureAsciiDirectory`（运行时软故障，需逐点排查）
4. **EncodingDetector**：Linux ICU 环境的 GB 编码检测行为差异
5. **通知**：Linux 桌面通知（freedesktop org.freedesktop.Notifications，notify-send 或 D-Bus）
6. **验证器迁移评估**：`PCL.Core.Wpf/Utils/Validate/*`（FluentValidation 封装，无 WPF 依赖）可考虑移入 PCL.Core 以恢复 11 个测试

## 分支

`feat/avalonia-mvp`（基于 dev @ 74c4e2d7）
