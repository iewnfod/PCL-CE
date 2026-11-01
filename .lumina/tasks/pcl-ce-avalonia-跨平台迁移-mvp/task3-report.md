# 任务 3 报告：Avalonia 壳与应用骨架

## 验收结果

- **Avalonia 壳在 Linux 成功启动**：窗口经 xdotool 确认真实创建（XWayland），消息循环稳定运行（timeout 杀进程验证存活）
- **完整生命周期跑通：21 个服务全部启动成功，零失败**（Java 扫描、配置存储、RPC 管道、本地化 zh-CN、主题 gsettings、遥测等）
- 回归：PCL.Core 双目标 0 错误；Linux 测试 192 通过（基线持平）；WPF 主程序 0 错误

## 新增内容

### PCL.Avalonia 项目（net10.0，Avalonia 11.3.2）
| 文件 | 职责 |
|---|---|
| `Program.cs` | 启动装配：ApplicationService/MainWindowService.Loading、PlatformServices 全量注册、`Lifecycle.OnInitialize()` |
| `App.axaml(.cs)` | Fluent 主题起步；窗口创建交给 MainWindowService（避免与生命周期冲突） |
| `MainWindow.axaml(.cs)` | 左侧导航 + 内容区骨架布局（FormMain 同构） |
| `Platform/AvaloniaHosts.cs` | IApplicationHost（ClassicDesktopStyleApplicationLifetime）、IWindowHost、IUiDispatcher、IThemeBackend、IClipboardService、IDialogService、IResourceStore 的 Avalonia 实现 |
| `Platform/AvaloniaLocalizationBackend.cs` | **XDocument 直接解析 WPF 语言 XAML**（sys:String 提取），复用上游语言文件无需转换 |

已加入 `Plain Craft Launcher 2.slnx`。

## 关键修复（核心层，WPF 版同样受益）

### 1. Hint 日志风暴（致命，阻塞壳启动）
**根因链**：`LogLevel.Warning = 300 + ActionLevel.HintErr` → 每条 Warning 默认弹 hint → 无 presenter 时 hint 被丢弃并产生新 Warning → 无限递归、消息嵌套膨胀，20 秒内 13 万条日志炸爆通道。
**修复**：`LogService._LogAction` 在 `HintService.Presenter is null` 时跳过 hint 动作（降级纯日志）。

### 2. DependencyGroups 空组误报（WPF 版既有 bug）
`InvokeInjection` 把「该 target 分组无成员」（如 rpc-property 无属性标注）当注入失败 → 每次启动误报 `Dependency injection failed: rpc-property@128`。修复：空组跳过。

### 3. 重复的 SystemThemeHelper
`PCL.Core.UI.Theme.SystemThemeHelper` 与 `Utils/OS/SystemTheme` 重复实现且未平台化（Linux 上抛异常导致主题服务启动失败）→ 委托唯一实现。

### 4. DependencyCheckService 平台守卫
PowerShell `Get-AppxPackage`（微软商店依赖包检查）仅 Windows；非 Windows 跳过。

## 已知限制（后续任务处理）

- `IUiModalFrame` 为占位实现（Avalonia 无 PushFrame 等价物）——同步弹窗在控件库阶段改为异步等待
- 中文字体策略暂用系统默认（控件库阶段统一）
- Tmds.DBus 0.21.2 有已知漏洞告警（Avalonia 间接依赖，关注上游升级）
- 自绘标题栏/动画背景等待控件库任务
