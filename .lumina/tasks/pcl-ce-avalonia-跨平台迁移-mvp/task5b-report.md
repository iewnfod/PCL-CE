# 任务报告：第二批控件 1:1 复刻（MyIconButton / MyListItem / MyCheckBox / MyHint + SvgIcon）

## 新增控件（PCL.Avalonia/Controls）

| 控件 | WPF 源 | 复刻要点 |
|---|---|---|
| `SvgIcon.cs` | PCL.Core.Wpf SvgIcon | AvaloniaResource 链接共享 88 个 lucide 图标；解析 path/circle/ellipse/line/rect/polyline/polygon 全元素；stroke 线条渲染（宽 2、round cap/join、viewBox 24）；静态缓存 |
| `MyIconButton.cs` + 主题 | 496 行 | 圆形按钮，Color/White/Black/Red/Custom 五主题（半透明降调用 Opacity 0.63 模拟 alpha 160，悬停恢复）；按下 0.8 缩放 + OutBack 样条回弹；宽度未设时取高度 |
| `MyListItem.cs` + 主题 | 1321 行 | 六列网格（勾选条/缩进/Logo/标题/信息/右距）；悬停 Bg1 浮层 + 右侧按钮淡入 + 右列扩展；按下 ColorBrush6 + 0.98 缩放（scaleAnim 可关）；勾选条高 20 弹性展开；RadioBox 父容器互斥（0 选中自动选第一个）；Logo 四源：SvgIcon/http（缓存+异步）/本地位图/矢量路径；Tags 标签气泡（Lang 本地化键求值）；事件可取消（Changed handled 回滚，WPF 同款） |
| `MyCheckBox.cs` + 主题 | 498 行 | 18×18 方框（边 1.1、圆角 3）+ 12×12 对勾 + 10×10 不定态方块；三态循环 false→true→null；对勾 300ms OutBack 弹出；按下 16.5 缩放 + Bg1 底；PreviewChange 可取消 |
| `MyHint.cs` | 205 行 | 继承 Border（WPF 同构）；Red/Blue/Yellow = HSL 355°/210°/40°，背景 (hue,90,L7)、边框文字 (hue,90,L2)；FromHsl2 感知补偿算法 1:1 搬运（cent 表 14 段）；CanClose 写 RelativeSetup 配置后不再显示；关闭动画（0.92 缩放+淡出+高度收起） |
| `RouteEventArgs.cs` | Common.RouteEventArgs | User/Handled，供 MyListItem/MyCheckBox 取消语义 |

## 关键修复（过程发现）

1. **csproj AvaloniaResource Link 通配符**：`Link="Assets\**\*.svg"` 中 `**` 不展开，资源被嵌成字面路径 → 改 `%(RecursiveDir)%(Filename)%(Extension)`
2. **SVG 圆形渲染**：单段 ArcSegment 起终点重合会被 Skia 丢弃 → 改两段半圆弧拼合
3. **选中前景色**：构造期 DynamicResource 在主题资源注入前解析失败 → 改 TryGetResource 同步取值 + AttachedToVisualTree 补刷新（WPF SetResourceReference 语义）
4. **命名冲突**：`SvgIcon` 属性名遮蔽控件类型（别名解决）、`Avalonia.Controls.Shapes` 解析歧义（PathShape 别名）、`Theme` 隐藏基类（new）

## 验证（代码级 + 视觉）

- **编译**：PCL.Avalonia 0 错误；全解决方案 0 错误；Core 测试 193 通过（12 失败为基线既有，本次 Core 零改动）
- **代码级诊断**（PCL_DIAG）：资源全就绪；MyListItem 选中 `Foreground=#ff021f77`（=ColorBrush2）、伪类 `:checked,:scaleAnim` 正确；MyHint `Background=#fffee6e8 / Border=#fff42b3c`（HSL 正确）；MyCheckBox 选中蓝框
- **悬停状态机**（程序化 PointerEntered/Exited）：`enter→ over=True、backOp=1（ColorBrushBg1）、按钮区 Opacity=1；leave→ 全部复位` ✓
- **视觉（vision 对照）**：图标全渲染（circle-check 圆圈闭合）、三色提示条正常、复选框三态正常、列表蓝字+竖条选中态确认
- **发现的环境限制**：GNOME Wayland 禁止 xdotool XTEST 移动鼠标——悬停无法截图验证，已用程序化事件链路验证替代（真实鼠标事件本身正常，任务 4 点击已证明）

## 已知降级（与 WPF 的差异）

- 右列宽展开无平滑动画（GridLength 无 Transition，直接跳变 + 按钮淡入）
- MyCheckBox 勾选瞬间无 18→12→18 呼吸动画（仅对勾弹出）
- 网络图片 Logo 无皮肤头像 NearestNeighbor 特判（统一高质量缩放）
- SvgIcon 单色渲染（lucide 全系单色 stroke，无影响；多色 SVG 图标包未来再议）
- MyIconButton/MyHint 的 `Theme` 属性 `new` 隐藏基类 ControlTheme 属性

## 下一批建议（任务 6「主页与实例选择」前置）

按使用频率剩余：MyScrollViewer(100)、MyIconTextButton(45)、MyMenuItem(32)、MyRadioBox(29)、MyTextButton(25)、MySlider(22)、MyImage(12)、MySearchBox(8)、MyExtraButton(7)
