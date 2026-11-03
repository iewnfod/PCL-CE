# 任务 8 报告：基本设置页

## 验收结果

> **设置持久化、重启生效，内存参数正确传入 JVM 参数生成** —— 全部 ✓

- **设置持久化**：`PCL_SETUP_APPLY="mode=1,ram=13,light=2,filesource=2"` 应用后，`config.v1.yml` 落盘 `LaunchRamType: 1 / LaunchRamCustom: 13`，共享 `config.v1.json` 落盘 `UiLightColor: DeathBlue / ToolDownloadSource: 2`
- **重启生效**：重启后 UI 恢复——「自定义」选中、最大内存 2.0 GB、滑块可用（vision 确认）；主题/下载源同步恢复
- **内存参数进入 JVM**：`--launch 1.21.11-Fabric_0.20.1` 实测启动日志 **`-Xmx2048m` + `-Xmn307m`**（tick 13 → 2.0GB → 2048m，新生代 2048×0.15=307 ✓），游戏进程成功启动
- **测试基线**：PCL.Core.Test **192 通过 / 13 失败，与任务 6/7 基线完全一致**（stash 对照确认 FileConfigStorageTest 失败为既有）；全解决方案（含 WPF）构建 0 错误

## 新增文件

| 文件 | 说明 |
|---|---|
| `PCL.Avalonia/Controls/MySlider.cs` | PCL 滑块（复刻 WPF MySlider）：16px 轨道、1px 底线 30%、2px 前景、10px 圆点悬停放大、拖动气泡、←/→（Ctrl×10）键盘 |
| `PCL.Avalonia/Controls/MyComboBox.cs` | PCL 下拉（复刻 WPF MyComboBox 选择模式）：27px 圆角 4、SemiTransparent 底、悬停边框转主题色、V 箭头旋转动画、白底弹层卡片（选中项 Bg1 底主题色字） |
| `PCL.Avalonia/Pages/PageSetupLeft.cs` | 设置页左栏导航（SetupPage 枚举 ×11 项，分组对齐 WPF；显式互斥选中） |
| `PCL.Avalonia/Pages/PageSetupLaunch.cs` | 启动页：启动选项（可见性下拉，标签左/控件右）+ 游戏内存（纵向圆点单选、-Xmx/-Xms 滑块、6px 三段内存条：上小灰字标签/下深色+主题色粗体数值、内存过多 MyHint）+ 启动器行为（显示"你知道吗"/锁定窗口大小） |
| `PCL.Avalonia/Pages/PageSetupJava.cs` | Java 页：独立「添加/刷新」图标文字卡（对齐 WPF MyIconTextButton）+ 列表（自动选择 + 扫描结果单选、64Bit/发行商标签、打开文件夹/详情 MyMsgBox/启停用悬停按钮、StorageProvider 手动添加）|
| `PCL.Avalonia/Pages/PageSetupManage.cs` | 管理页：文件下载源/版本列表源下拉（镜像/官方优先/官方）直写 Config |
| `PCL.Avalonia/Pages/PageSetupAppearance.cs` | 个性化页：配色模式（浅/深/跟随系统）+ 浅/深主题色色块（LabColor Lch 同算法预览色、白色描边选中+文字加粗变主题色），写 Config 后 ThemeService 自动全局刷新 |
| `PCL.Avalonia/Platform/AvaloniaMemorySettings.cs` | 真实内存策略（1:1 复刻 WPF PageSetupLaunch/PageInstanceSetupViewModel）：实例级 MemorySolution（2 全局/0 动态/1 自定义）→ 全局模式（自动四阶段曲线含 Mod 数估算/自定义刻度 GetRamFromTick）、32 位 Java 上限 1G、-Xms 完整语义（锁定内存/自定义 -Xms/仅手动模式） |

## 关键修改

| 文件 | 说明 |
|---|---|
| `MainWindow.axaml.cs` | `_ShowSetupCore()`（左栏 + 子页切换 + 占位卡）；`PCL_SETUP_APPLY` 一次性设置钩子（mode/ram/initial/light/dark/colormode/filesource/versionsource/visibility，供自动化验证）；`PCL_SETUP_PAGE` 直达子页 |
| `UiBridges.cs` | `DefaultMemorySettings`（固定 40%）→ `AvaloniaMemorySettings`（真实配置驱动） |
| `MyListItem.cs` | **修复既有 bug**：PlainTags 多标签堆叠（Panel → StackPanel 横排） |
| `MyRadioButton.cs/.axaml` | 新增 `RadioBox` ColorType（页面圆点单选，1:1 WPF MyRadioBox：14px 圆环、选中蓝环+内点、透明底） |
| `PCL.Core/.../FileConfigStorage.cs` | **修复核心层缺陷**：写通道空闲时攒批永不落盘（ReadAsync 无限阻塞、Sync 仅由下个写入驱动）→ 合并窗口内带超时等待，窗口期满自动 Sync（写入后最多 10s 必落盘，防抖语义不变）。此前非优雅退出（SIGKILL/断电）会丢最后一次攒批 |

## UI（对照用户提供的 Windows 版截图逐项修正）

- **启动选项**：标签左、控件右两列（对齐 WPF 表单行）
- **游戏内存**：单选从胶囊改**纵向圆点**（新增 RadioBox 样式）；内存条 18px→**6px 细条**；条上方小灰字（已使用/已安装 ｜ 游戏分配）、条下方左深色粗体 `21.4 / 30.5 GiB` + 右**主题色**粗体 `2.0 GiB`（对齐 WPF 双色数值）
- **Java 页**：「添加」改**独立小卡**（⊕ 图标+文字，对齐 WPF MyIconTextButton）+ 刷新同卡；标签横排修复
- **主题页**：色块 30→34px + 下方文字标签（天空蓝/龙猫蓝/死机蓝）、选中白色描边+文字主题色加粗
- **下拉框**：边框 SemiTransparent→Gray5（白卡上可见），箭头 chevron-right 旋转 90°（图标包无 chevron-down）

## 关键调试过程

1. **xdotool 合成事件在 XWayland 不可达**（任务 7 已知限制复现）→ 沿用环境变量钩子模式（PCL_SETUP_APPLY），并修正钩子位置（构造器独立调用，不再依赖设置页打开）
2. **`dotnet run --` 参数丢失**（命令行参数为空）→ 直接执行产物二进制传 `--launch`
3. **配置写入不落盘**：诊断日志证实 hook 的 SET 入队后，旧代码在通道空闲时阻塞于 ReadAsync、Sync 永不触发 → FileConfigStorage 超时窗口修复（前述）
4. **`Config.Instance.*`（GameInstance 源）要求非空实例路径参数**：无选中实例时 DynamicCacheConfigStorage 抛 ArgumentNullException → AvaloniaMemorySettings null 降级走全局
5. **AppDomain.ProcessExit 在 pkill -9（SIGKILL）下不执行**：优雅落盘依赖修复后的窗口到期 Sync 而非退出钩子

## 已知限制（后续任务）

- 启动选项卡的其余行（隔离/窗口标题/优先级/正版验证/IP 协议）与高级启动选项卡未做（MVP 范围外，WPF 对照可见）
- 下载线程/限速滑块、社区资源获取行为卡未做（任务书仅要求下载源）
- 主题切换目前即时生效（ConfigEvent → ThemeService），暗色模式的全套资源在 Avalonia 侧的覆盖率待任务 10 打磨
- 「锁定窗口大小」等行为开关仅持久化，窗口行为接线待后续任务
- xdotool 交互验证不可用，视觉验收基于 vision 对照 Windows 参考截图
