<!-- lumina: session=ses_e69526860ffeyN7gOX9F1qphHg -->

# PCL CE → Avalonia 跨平台迁移计划（以 MVP 为目标）

## 目标

在保留现有 WPF 版本不动的前提下，基于 `PCL.Core` + `PCL.Launcher.Core` 两个零 WPF 依赖的核心层，新建 Avalonia UI 实现 **Linux 上日常可用的 MVP**：

> **MVP 验收标准：在 Linux 上打开启动器 → 安装/选择一个 MC 实例 → 离线或微软账户登录 → 点击启动成功进入游戏，全程 GUI 操作无致命错误。**

## 非目标（本期不做）

- 下载市场/Mod 管理/整合包/帮助页/音乐播放/视频背景（Alpha 阶段再议，业务层已就绪）
- macOS 适配（架构上顺带保持可行，不投入验证）
- FlatPak/AppImage 打包与签名（MVP 末期视情况提供自包含发布目录）
- WPF 版任何行为变更

## 技术决策

| 决策点 | 选择 | 理由 |
|---|---|---|
| UI 框架 | Avalonia 11（NuGet） | 架构注释已预留；X11 后端经 XWayland 在 GNOME Wayland 开箱即用 |
| 核心层改造方式 | `net10.0-windows` → 多目标/改 TFM + `OperatingSystem.IsWindows()` 守卫，Windows 路径行为不变 | 现有 `Platform/` 抽象接口（IUiDispatcher/IThemeBackend/IDialogService 等 10 个）正是为此设计 |
| 平台服务落地 | 新建 `PCL.Platform.Linux`（或并入壳项目）提供 Linux 实现，启动时注入 `PlatformServices` | 与架构既有模式一致 |
| UI 项目布局 | 新建 `PCL.Avalonia`（App + 控件库 `My*` + Pages），RootNamespace 与现有 ViewModel 兼容，最大化复用 55 个现有 ViewModel | MVVM 层已完成拆分，是本次迁移最大资产 |
| 密钥存储 | Linux 用 keyfile（0600 权限）替代 DPAPI，预留 libsecret 接口 | 最小可用，不引入 keyring 强依赖 |
| 窗口监控 | WatcherWindow（user32）降级为纯日志监控，接口留好 X11 扩展位 | Wayland 无等价 API，MVP 不阻塞 |
| 视觉 | `AdaptiveBlur.ps` 等 ShaderEffect 用 Skia/组合效果近似或降级 | 接受有限保真度差 |

## 任务详情（执行顺序即里程碑）

### 阶段 0：核心层编译验证（Walking Skeleton 的第一步）

**1. 核心层多目标编译验证**
- `PCL.Core`、`PCL.Launcher.Core`（及 `PCL.Core.Test`）改为可跨平台编译（多目标或条件 TFM），Windows 专属 NuGet（System.Management、ProtectedData 等）加条件引用
- 逐文件加 `OperatingSystem.IsWindows()` 守卫直到 `dotnet build` 在 Linux 全绿
- 运行 `dotnet test`，记录基线：哪些测试在 Linux 失败及原因（预期为注册表/WMI/DPAPI 相关，进入任务 2 修复）
- 产出：精确的 Windows 耦合点改造清单（比评估时的 grep 更准）

### 阶段 1：核心层 Linux 化

**2. 核心层 Linux 服务适配**
- Java 扫描：新增 Unix 扫描器（`which`/`update-alternatives`/`~/.sdkman`/常见路径），接入现有 `IJavaScanner` 管线
- 加密：`EncryptHelper` 的 DPAPI 路径加 Linux 分支（keyfile 0600）
- 硬件/系统信息：WMI → `/proc`、`lscpu` 解析或降级为 Unknown（不阻塞启动）
- 注册表依赖点逐一处理：系统主题（读 gsettings 或默认浅色）、区域策略、AUMID/通知（MVP 禁用）、GPU 偏好（跳过）
- 路径卫生：`LauncherPaths` 等处硬编码 `\`、`C:\`、8.3 短路径逻辑改 `Path` API / 平台分支
- 提权、单实例（NamedPipe→Unix socket）、开机自启等 Essentials 服务加 Linux 分支或降级
- 验收：Linux 上 `dotnet test` 核心用例全绿；用最小 console harness 调用启动核心，成功用系统 Java 21 启动一个 MC 实例（无 UI 验证）

### 阶段 2：Avalonia UI（最大头）

**3. Avalonia 壳与应用骨架**
- 新建 `PCL.Avalonia` 项目：App、主窗口（布局参考 FormMain：左侧栏+内容区）、DI/IoC 对接现有 `ApplicationService`/`MainWindowService`
- 实现 `Platform/` 接口的 Avalonia 版（IUiDispatcher、IThemeBackend、IDialogService、IClipboardService、IResourceStore、ILocalizationBackend 等）并注入 `PlatformServices`
- 主题系统：对接现有主题模型（NColor/主题色切换），中文本地化字体策略
- 验收：空壳应用在 Linux 启动，主题色/语言切换生效

**4. 启动游戏端到端（Walking Skeleton 达成点）**
- 最小页面：实例下拉列表 + 启动按钮 + 进度/日志展示
- 打通「选实例 → Java 选择 → Launch Core → Process 启动 → 日志监控（降级版 Watcher）→ 游戏窗口出现」
- 验收：**Linux 上从 GUI 启动一次真实 MC 成功**——本计划最关键里程碑，尽早证伪所有隐藏假设

**5. 基础控件库移植**
- 按依赖顺序移植高频 `My*` 控件：MyCard/MyButton/MyCheckBox/MyRadioBox/MyTextBox/MySlider/MyListItem/MyHint/MyLoading/MyMsgBox/MyToast/MyScrollViewer 等（PCL.Core.Wpf + 主程序 Controls 约 40 个，按页面实际用到的分批）
- 现有 ViewModel 的绑定/命令模式直接复用，控件 API 尽量同名以减少页面代码改动
- 验收：控件演示页，视觉与 WPF 版截图对照可接受

**6. 主页与实例选择**
- PageHomepage（公告/背景）+ PageSelectLeft/Right（实例列表、筛选、最近游玩）
- 验收：实例切换流畅、与 WPF 版功能对等（不含整合包）

**7. 实例安装与版本管理**
- PageDownload 中「版本安装」子集：版本清单、安装、删除、隔离设置；下载管线复用现有 Downloader（Linux 下 AdaptiveRangeDownloader 的 Win API 分支已去化）
- 验收：GUI 内全新安装一个原版/Forge/Fabric 实例并启动成功

**8. 基本设置页**
- PageSetup 子集：Java 路径（Unix 扫描器结果选择/手填）、内存分配、下载源、外观主题、启动器行为
- 验收：设置持久化、重启生效，内存参数正确传入 JVM 参数生成

**9. 账户登录**
- 离线登录完整流程；微软 OAuth（系统浏览器 + loopback 回调，纯 HTTP 跨平台）+ 凭据安全存储（任务 2 的 keyfile）
- 验收：微软账户登录、皮肤/档案显示、启动带入 AccessToken 成功进正版服（若你有多账户/正版号可测）

**10. MVP 联调打磨**
- 全链路回归：异常路径（Java 缺失、网络失败、实例损坏）的错误提示与日志
- 崩溃日志/反馈入口最小版；单实例互斥；自包含发布（`dotnet publish -r linux-x64`）验证产物可移植
- 验收：连续使用 30 分钟无致命错误，达到「日常可用」定义；MVP 完成

## 风险与应对

| 风险 | 应对 |
|---|---|
| 上游 dev 分支快速演进造成 Core 层合并冲突 | Core 层改动保持小而克制（守卫式），定期 rebase；UI 层全新目录天然隔离 |
| 编译验证发现的 Windows 耦合远超预期 | 任务 1 产出的精确清单会第一时间暴露；若超标则收缩 MVP 范围（如暂缓 OAuth） |
| Avalonia 控件保真度不足 | 每批控件配截图对照验收，接受降级；动画用 Avalonia Transition 重写 |
| OAuth 回调被浏览器/防火墙干扰 | loopback 端口方案三平台通用；备选设备码流程（代码已有 DeviceCodeData） |
| Wayland 特有问题（拖放/输入法） | MVP 统一走 XWayland；原生 Wayland 后端列为已知限制 |

## 工作方式

- 每个任务完成后 `task_complete` 汇报，重大视觉/交互决策在对话中贴截图或说明征求你的确认
- 任务 4（端到端启动）到达时若你方便，请实际跑一次游戏验证
- 所有改动都在本仓库 dev 分支或独立 feature 分支上进行（届时征求你意见）
