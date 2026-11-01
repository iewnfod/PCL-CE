# 任务 2 报告：核心层 Linux 服务适配

## 验收结果

- **Console Harness 全链路验证通过**：UnixJavaScanner + UnixJavaParser 在本机发现并正确解析 **9 个 Java**（Dragonwell 21、openjdk 8/17/21/25/27、default×2、/usr/bin/java），版本/品牌/位数/架构全部正确；内存(30GiB)/CPU(24核)/区域(Mainland)/深色模式/路径抽象全部正常
- Linux 测试：192 通过 / 13 失败（与任务 1 基线持平，均为已归类既有问题；TestJavaSearch 由改后通过）
- Windows 目标与主程序交叉编译：0 错误

## 适配内容

### 运行时守卫（"Linux 跑不炸"）
| 组件 | 方案 |
|---|---|
| `KernelInterop` | 全面平台分支：内存→/proc/meminfo；P-core 数→ProcessorCount；管道对端 PID→0（白名单拒绝语义保留）；ExitProcess→Environment.Exit；控制台→no-op；GetACP→0 |
| `WatcherWindow` | user32 窗口枚举仅 Windows；Unix 降级为纯日志进度监控 |
| `LauncherCommon.ShortenPath` | 8.3 短路径仅 Windows；Unix 直接返回原路径 |
| `SystemTheme` | 注册表→`gsettings get ... color-scheme`（prefer-dark） |
| `RegionPolicy` | 新增 `ReadUnixSignals()`：LC_ALL/LC_MESSAGES/LANG + IANA 时区（Asia/Shanghai 已在 ChinaTimeZones） |
| `ToastNotification`/`AumidHelper` | 任务 1 已守卫，确认无其他调用点 |
| `NamedPipe` 系列（RPC/Promote/单实例） | **无需修改**：System.IO.Pipes 在 Linux 映射 Unix domain socket，天然跨平台 |

### Java 体系（本任务核心）
- 新增 `UnixJavaParser`：执行 `java -version` 解析版本/品牌/64位/架构；修复两个解析 bug（5 段版本号截断、`1.8.0_504` 下划线后缀）
- 新增 `UnixJavaScanner`：`command -v java` + `ResolveLinkTarget` 软链解析（alternatives）、/usr/lib/jvm、/opt、~/.sdkman、~/.jdks、~/.minecraft/runtime
- `JavaInstallation`/`JavaConsts`：可执行文件名平台化（java.exe/java），Unix 无 javaw
- `PathEnvironmentScanner`：`Path.PathSeparator` 替代硬编码 `;`；`DefaultPathsScanner`：平台文件名 + /usr/lib/jvm 根
- `JavaService`：按平台装配 parser 与扫描器组合
- **`JavaManager._ShouldEnableByDefault`：Unix 可用性检查改为 libjvm.so**（原 jvm.lib/rt.jar 检查导致 Unix 扫描出的 Java 全部默认禁用——实际功能 bug，非仅测试问题）

### 路径卫生
- `LauncherPaths`：ExecutableDirectory/Temp/PureAsciiDirectory 改用 `Path.DirectorySeparatorChar`；Unix 下 PureAsciiDirectory = 系统临时目录（无 8.3/GBK 问题）
- `SystemPaths.DriveLetter`：Unix 返回 "/"

## 遗留（任务 3+）

- `Basics.Metadata` 要求入口程序集嵌入 `PCL.metadata.json`（Avalonia 壳需提供；harness 已用 EmbeddedResource 验证方案可行）
- 完整 `GameLaunch` 链依赖 LaunchUi/Config/States/InstanceList 服务初始化——任务 3 壳骨架就绪后、任务 4 端到端打通
- EncodingDetector GB 编码 ICU 行为差异（低优先级）
