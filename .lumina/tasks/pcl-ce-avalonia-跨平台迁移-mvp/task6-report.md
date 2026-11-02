# 任务 6 报告：主页与实例选择（Avalonia UI 复刻）

## 验收结果

- **主窗口 FormMain 同构重构**：自绘标题栏（48px 主题色 + PCL 白色线条 logo + 「Linux」白底徽章 + 4 导航 MyRadioButton + 最小化/关闭）+ 左栏背景/右缘阴影 + PanMainLeft/PanMainRight 双页容器 + 子页标题模式（进入实例选择后中间导航替换为「← 实例选择」，对应 WPF PanTitleInner）
- **真实数据链路跑通**（非 fake）：GameFolders.McFolderListLoader 驱动文件夹列表 → 选中切换 → InstanceList.mcInstanceListLoader 驱动实例卡片（分组/标题计数/折叠/搜索过滤/收藏/打开文件夹），点击实例 → McMcInstanceSelected → 返回启动主页
- **启动主页**：登录区（头像占位 + Player/离线账户/切换账户）、启动按钮（54 高 Highlight + 按钮内底部版本名，无实例时「下载游戏」+副标题——与 Windows 版一致）、进度条；右侧社区公告卡 + Minecraft 官方信息流瀑布流（真实 NewsViewModel + 网络图片异步加载）+ 启动日志卡（UiBridgeHub 日志流）
- **视觉验证**（vision 对照，多轮迭代后）：标题栏导航选中白胶囊 ✓、左栏列表选中蓝字+竖条 ✓、搜索框（放大镜+清除+占位「搜索游戏实例」）✓、卡片描边/阴影轮廓 ✓、实例标签气泡 ✓、fake 双实例正确分入「错误的实例 (2)」折叠组 ✓

## 新增文件（PCL.Avalonia）

| 文件 | 说明 |
|---|---|
| `Controls/MyRadioButton.cs` + `Themes/MyRadioButton.axaml` | 标题栏导航按钮：高 27 胶囊（圆角 13.5）、White/Highlight 双主题（选中白底主题色字）、父 Panel/GroupName 互斥 |
| `Controls/MySearchBox.cs` | 搜索框：高 40 圆角 5 卡片 + 放大镜 + 清除按钮 + Watermark |
| `Controls/WaterfallPanel.cs` | 瀑布流：最短列放置、MaxItemWidth 240、列/行距 14 |
| `Controls/UiResources.cs` | `SetDynamic()` 扩展（WPF SetResourceReference 等价，经 `Bind(prop, DynamicResourceExtension)`） |
| `Pages/PageSelectLeft.cs` | 文件夹列表（RadioBox 40 高 + 路径 Info + 设置按钮 + 添加/导入区） |
| `Pages/PageSelectRight.cs` | 搜索 + 分组实例卡（MyCard CanSwap + 42 高实例项 + 版本标签 + 收藏/打开/删除/设置按钮）+ 空/搜索空/加载三态 |
| `Pages/PageLaunchLeft.cs` | 登录区 + 启动按钮（GameLaunch.McLaunchStart 集成）+ 实例选择/设置按钮 |
| `Pages/PageLaunchRight.cs` | 公告卡 + 新闻瀑布流（图片异步缓存）+ 日志卡 |
| `AppUi.cs` | 静态导航（MainTab + 启动子页切换） |

## 关键修复（过程发现）

1. **核心层 Linux 路径 bug 三处**（WPF 版不受影响，`Path.Combine` 在 Windows 产生 `\`）：
   - `GameFolders._ScanOriginalFolders`：`versions\` / `folder.FullName\` 硬编码反斜杠 → Linux 扫描永远为空（**这是文件夹列表空的根因**）
   - `GameFolders._EnsureMinecraftFolder`：同上
   - `GameFolders._LoadCustomFolders`：配置项 `EndsWithF(@"\")` → 兼容 `/` 结尾
   - `InstanceList.Scan`：`$@"{folder.FullName}\"` 实例路径 → Linux 上所有实例 state=Error
2. **`Lifecycle.When` 语义坑**：状态已达时静默忽略回调；且 `Program.Main` 的 `OnInitialize()` 内部阻塞至退出——FolderLoader 启动移至 MainWindow 构造器（与 WPF FormMain 完全同构）
3. **Avalonia `DynamicResourceExtension` 代码用法**：直接赋值属性抛 InvalidValue；必须经 `Bind(property, extension)`（XAML 的 `[!Prop]` 语法由编译器转换，手写 C# 无此糖）
4. **MyCard 阴影不渲染**：`Background=Transparent` 时 Avalonia 不绘制 BoxShadow → `#01000000` 近透明底；内容区双倍偏移（模板 40px 标题行 + 内容自带 top margin）→ 去重；折叠箭头改为 CanSwap=true 才显示（对齐 WPF）
5. **AvaloniaHintPresenter**：`BoxShadows.Parse("0 2 6 0")` 在新版解析格式变化 → 改对象初始化
6. **导航重入**：_SyncNavChecked 触发 Check 事件递归 → _syncingNav 防护
7. **MC 实例目录约定**：`ExecutableDirectory/.minecraft`（核心层约定），非 `PCL/.minecraft`

## 验证

- **编译**：PCL.Avalonia 0 错误；全解决方案（含 WPF 主程序）0 错误
- **测试**：PCL.Core.Test 192 通过 / 13 失败——stash 对照确认新增失败与本改动无关（缓存/网络/语言文件为基线既有）
- **运行**（GNOME Wayland/XWayland，vision 多轮对照）：
  - 主页：标题栏/导航/登录区/启动按钮/公告卡/新闻瀑布流（真实图）全部渲染正常
  - 实例选择：PCL_NAV=select 直开验证；fake 双实例（bin 下 .minecraft）正确渲染分组卡片
  - 端到端导航流：点实例 → 选中 → 自动返回主页（堆栈跟踪确认设计行为）
  - 与 Wine 下运行的 Windows 版 PCL（用户参照）逐项对照修正了：无实例按钮文案、子页标题栏、占位 tab 分组结构（下载/设置/工具左栏文案与分组）

## 已知限制（MVP 范围外/后续任务）

- 下载/设置/工具 tab 为占位页（分组左栏 + 「后续版本提供」卡）——任务 7/8 实现
- 登录区为档案展示 + 占位「切换账户」——任务 9 实现真实登录
- 页面切换无动画（WPF PageChangeAnim）——动画批次统一做
- 左栏文件夹右键菜单（改名/上移/下移等）未实现（设置按钮暂代打开文件夹）
- WPF 毛玻璃 BlurBorder 以极淡描边近似
- 无实例 Logo 时显示空白占位（WPF 用 RedstoneBlock 默认图）
