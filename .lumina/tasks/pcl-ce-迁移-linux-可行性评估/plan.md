<!-- lumina: session=ses_e69526860ffeyN7gOX9F1qphHg -->

# PCL CE 迁移 Linux 可行性评估报告

## 一、结论（TL;DR）

**技术上可行，且该 fork 的架构重构方向明确指向这条路，但 UI 层需要近乎完整重写，是主要工作量所在。** 综合评估：

| 维度 | 评价 |
|---|---|
| 架构准备度 | ★★★★☆ 分层与平台抽象已就位，`Platform/` 接口注释明确写着「WPF/Avalonia 分别实现」 |
| 业务核心跨平台性 | ★★★★☆ 启动器本质业务（下载/版本管理/Java 启动/OAuth）天然跨平台 |
| UI 迁移成本 | ★☆☆☆☆ ~8.3 万行 WPF 代码 + 102 个 XAML + 40 余个自定义控件需移植到 Avalonia |
| 估计工作量 | 熟练小团队 3–6 个月出可用 alpha；单人业余 6–12 个月 |

## 二、现状架构（对迁移有利的证据）

代码库已拆为 4 层（共 ~15.6 万行 C#）：

| 项目 | 规模 | WPF 耦合 | 迁移难度 |
|---|---|---|---|
| `PCL.Core` | 443 文件 / 3.8 万行 | **0 个 WPF 引用**；TFM 仍为 `net10.0-windows` | 低：去 Windows 化即可 |
| `PCL.Launcher.Core` | 173 文件 / 3.6 万行 | **0 个 WPF using**（`Ui/` 目录实为 ViewModel） | 低 |
| `PCL.Core.Wpf` | 151 文件 / 1.2 万行 | WPF 专属适配层 | 高：需 Avalonia 等价物 |
| `Plain Craft Launcher 2` | 276 文件 / 7.1 万行 + 94 XAML | WPF 主程序，已完成 MVVM 化（55 个 ViewModel） | 高：按页面移植 |

关键证据：
1. **`PCL.Core/Platform/` 已定义 10 个平台抽象接口**（IUiDispatcher、IThemeBackend、IClipboardService、IDialogService、IFileDialogService、IResourceStore、ILocalizationBackend、IApplicationHost、IWindowHost、IUiModalFrame），且多处注释明说「WPF 与未来的 **Avalonia** 容器由 UI 层各自实现」——上游维护者已在为跨平台铺路。
2. **最近提交**即为「完成 WPF/Core 拆分与业务层迁移」「UI 完全 MVVM 化并重构到 DialogService」——架构准备正在活跃进行。
3. **README 已声明支持 macOS/Linux 交叉编译**（仅编译不能运行）；代码中已出现 `OperatingSystem.IsLinux()`、`GetCurrentOsName()` 等跨平台分支。
4. **Java 扫描已接口化**（`IJavaScanner` + 5 个实现含 `WhereCommandScanner`），Linux 侧只需新增 Unix 扫描器。

## 三、Windows 耦合点清单（迁移阻力）

### 3.1 PCL.Core（~10 个文件需适配）
- **WMI（System.Management）**：`HardwareInfo`、`ProcessInterop`、`Secret/Identify(Old)`（机器指纹）→ 改读 `/proc`、`dmidecode`、`sysfs`，或用 `OperatingSystem` 守卫降级
- **DPAPI**：`Secret/EncryptHelper`（CNG/CAPI 双版本加密用户密钥）→ Linux 换 libsecret/Secret Service 或 keyfile(0600)
- **注册表**：`SystemTheme`、`RegistryChangeMonitor`、`App/Region/RegionPolicy`、`Minecraft/Java/Scanner/RegistryJavaScanner` → 主题读 GTK/gsettings，Java 扫描换 `which`/`update-alternatives`/常见路径
- **Win32 P/Invoke**：`KernelInterop`、`NtInterop`、`WindowInterop`（DPI/DWM）、`Utils/WinRT/WinRTInterop`（combase，WinRT 通知）→ Linux 用 XDG portal / freedesktop 通知，或直接禁用对应功能

### 3.2 PCL.Launcher.Core（3 个文件）
- `Minecraft/Watcher/WatcherWindow.cs`：user32 监控 MC 窗口标题（进度/崩溃）→ X11 可用 XDoTool 类方案，**Wayland 下无法等价实现**，建议降级为纯日志监控
- `LauncherCommon.cs`：`GetShortPathName`（8.3 短路径）→ Linux 不需要，直接返回原路径
- **隐蔽软依赖**：`LauncherPaths` 等处硬编码 `\` 拼接、`C:\` 假设、`PureAsciiDirectory` 逻辑 → 编译不报错、运行时才炸，需专项清理

### 3.3 UI 层（重写大头）
- 94 + 8 = **102 个 XAML** + ~40 个 `My*` 自定义控件（MyCard/MyButton/MyMsgBox/MyToast/动画背景等）
- **WPF ShaderEffect 像素着色器**（`AdaptiveBlur.ps`）→ Avalonia 无兼容路径，需用 Skia/Compositor 重写效果
- **NAudio** 音乐播放（522 行 Music 模块）→ Linux 需换 LibVLCSharp 等
- **System.Drawing**（图标/皮肤处理，5 处）→ ImageSharp 替代
- **Markdig.Wpf**（帮助页 Markdown）→ 换 Avalonia Markdown 社区包

### 3.4 系统集成（各有 Linux 对应物）
提权 runas→polkit / 单实例 NamedPipe→Unix socket（`SingleInstanceService` 已存在）/ 开机自启注册表→`.desktop` / 自更新（`IUpdateSource` 已抽象，AppImage 或自替换）/ 拖放 shell32→Avalonia 内建 / 打开目录→`xdg-open`

## 四、建议路线（分四阶段）

- **阶段 0（1–2 周）· 编译验证 spike**：`PCL.Core`、`PCL.Launcher.Core` 多目标 `net10.0`，Win API 加 `OperatingSystem.IsWindows()` 守卫，在本机 Linux 跑 `dotnet build` + `dotnet test`（已有 50 个测试文件），得到精确改造清单
- **阶段 1（2–4 周）· 核心库去 Windows 化**：完成 3.1/3.2 清单，核心层在 Linux 全绿
- **阶段 2（2–5 个月）· Avalonia UI**：新建 `PCL.Avalonia` 壳，先移植 My* 控件库，再按「主页→启动→设置→下载→实例」顺序逐页迁移（MVVM 层可直接复用）
- **阶段 3（2–4 周）· Linux 桌面集成**：打包（AppImage/FlatPak）、通知、文件关联、中文输入法验证、Java 检测

## 五、主要风险

1. **UI 保真度**：PCL 的动画/阴影/模糊等视觉深度定制在 Avalonia 有差距，需接受一定降级
2. **Wayland**：窗口监控（WatcherWindow）无等价 API；输入法/拖放在 Wayland 沙箱（FlatPak）下另有限制
3. **双平台维护成本**：上游 PCL CE 仍在快速迭代（近期大量 UI 重构），分叉越久同步越难——建议以「上游 PR + feature flag」而非硬分叉推进
4. **Linux 发行版碎片化**：Java 检测、字体（中文字体缺失会豆腐块）、glibc 兼容需要测试矩阵

## 六、替代方案（若不想重写 UI）

- **无头 CLI 版**：复用两个 Core 层做命令行启动器（工作量 ~阶段 0+1，2–6 周），但失去 PCL 核心价值（GUI），仅适合作为过渡产物
- **等待上游**：架构注释显示维护者有 Avalonia 意图，先做阶段 0/1 的上游 PR，与官方共建

---

## 批准后的执行任务

若批准本计划，将执行**阶段 0 验证性 spike**（不改动任何现有项目文件语义，仅新增多目标与平台守卫试验），用硬编译数据坐实评估结论，并把最终评估文档写入 `/home/muyunxi/.opencode/plan`：

1. **Linux 编译验证**：临时多目标化两个 Core 项目，统计并修复编译错误，运行 `dotnet test`
2. **评估文档定稿**：汇总 spike 数据，输出完整评估报告至 `/home/muyunxi/.opencode/plan/linux-migration-assessment.md`

> 注：spike 中的 TFM 改动属于验证性质，完成后可保留（作为阶段 1 起点）或按你意愿回退。
