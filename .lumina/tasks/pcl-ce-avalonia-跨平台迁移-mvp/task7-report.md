# 任务 7 报告：实例安装与版本管理

## 验收结果

> **GUI 内全新安装一个原版/Forge/Fabric 实例并启动成功** —— 原版 ✓ / Fabric ✓ / Forgelike 管线 ✓（受本机网络限制见下）

- **原版 1.21.11**：GUI 下载页自动选择版本 → 4655 个文件（json/libraries 62M/assets 442M/客户端 jar 30M）下载 → JSON 合并 → 隔离 mods/resourcepacks 文件夹 → `--launch` 启动 → **Minecraft 1.21.11 标题屏完整渲染**（vision 确认 MINECRAFT LOGO/菜单按钮/左下角 PCLC 标识）
- **Fabric 1.21.11-Fabric_0.20.1**：Fabric 版本选择 + **Fabric API 自动选择**（兼容性过滤 RawGameVersions）→ 安装 → 合并产物校验（id 正确/inheritsFrom 已移除/mainClass=KnotClient）→ mods 内 fabric-api jar 落盘 → **启动进游戏标题屏成功**
- **NeoForge 21.11.48**：管线全通（清单→下载→分析→wrapper 启动），Java 安装器（forge-installer.jar + JavaWrapper）在 Linux 运行至 `Downloading libraries` 后因 **maven.neoforged.net 本机不可达**（curl 验证 SSL reset）失败——网络环境限制，非代码缺陷（WPF 版同样失败）；手动 wrapper 测试确认机制正确

## 新增文件

| 文件 | 说明 |
|---|---|
| `PCL.Avalonia/Download/InstallPipeline.cs` | McInstall 入口/McInstallState/失败清理回滚/McInstallFolders/锁与 Identifier |
| `PCL.Avalonia/Download/InstallLoaders.cs` | McInstallLoader 组装 + McDownloadClientLoader（原版管线，Authlib hack 保留） |
| `PCL.Avalonia/Download/InstallMerge.cs` | 安装收尾 loader（合并+libraries/mods 拷贝+文件夹创建） |
| `PCL.Avalonia/Download/MergeJson.cs` | 多实例 JSON 合并（WPF 4 文件合一，LabyMod/OptiFine 分支裁剪） |
| `PCL.Avalonia/Download/FabricInstall.cs` | Fabric loader（meta 双源 fetch→profile json 写盘） |
| `PCL.Avalonia/Download/ForgelikeInstall.cs` | Forge/NeoForge/Cleanroom（modern Java 安装器 + legacy 解压 + injector 进程，3 文件合一） |
| `PCL.Avalonia/Pages/PageDownloadInstall.cs` | 两屏安装向导（版本清单屏/加载器选择屏）+ 自动化钩子 |
| `PCL.Avalonia/Pages/PageDownloadLeft.cs` | 下载页左栏导航（Minecraft 真实子页 + 刷新） |

## UI（对照用户提供的 WPF 参考截图）

- 屏 1：「最新版本」展开卡（正式版/快照图标区分）+ **四张默认折叠分组卡**（正式版 112/预览版 826/远古版 165/愚人节版 12，懒构建）
- 屏 2：**返回箭头+方块图标+带边框实例名输入框**（顶部卡）+ Fabric/Forge/NeoForge 状态卡（折叠态「· 可以添加」/无版本「· 无可用版本」/已选「Fabric：0.20.1」）+ Fabric API 自动安装提示行 + **底部居中「开始下载」按钮**（Highlight 描边样式与启动按钮一致）+ 安装进度卡（进度条+当前阶段名+完成态保留 5s）
- 交互：隔离确认弹窗（IndieSolutionV2=0/2 时）、实例名校验（非法字符/重复，禁用按钮+红字）、重复安装拦截、安装后台线程化（防 UI 死锁）

## 核心层修复（任务 2 路径卫生遗漏，过程中暴露）

1. **LauncherCommon.WriteFile 默认写 BOM**（`Encoding.UTF8` vs WPF `UTF8Encoding(false)`）+ **ReadFile 不剥 BOM** → 写回 clientVersion 后 JSON 解析炸 → 两处对齐 WPF 语义
2. **16 处 `versions\`/`PCL\` 硬编码反斜杠**（InstanceList/Cache、Modpack/*、McInstance.Check、CompFile.ToNetFile）→ Path API（Linux 下曾产生名为 `PCL\` 的垃圾目录、mods 文件名不拼接）
3. **HostConnectionHandler** 连接失败 Error→Debug（预期失败路径弹全局错误窗双报）
4. **WPF 原版 bug**：`_ResolveForgelikeInfo` switch 对「NeoForge/Cleanroom + info 非空」落 default throw（C# when-guard 语义，单测证实）→ 补分支

## 关键调试过程（Avalonia 特有坑）

- **TextBox.TextChanged 异步于 Text 赋值**（WPF 同步）→ `_isNameChanging` 防护失效 → 值比较兜底
- **--launch 不经过选择页**导致实例列表不加载 → `_OnReady` 主动 `LoaderFolderRun`
- **McInstall 在 UI 线程** + LogService Feedback 级弹窗 → 死锁 → `Task.Run` 后台化
- xdotool 合成事件在 Wayland 不可达 → 改用环境变量钩子（PCL_SELECT_VERSION/PCL_SELECT_LOADER/PCL_AUTO_INSTALL，与 PCL_NAV/--launch 模式一致）

## 验证

- **编译**：全解决方案（含 WPF 主程序）0 错误
- **测试**：PCL.Core.Test **192 通过 / 13 失败，与任务 6 基线完全一致**（TestEncoding/缓存/语言键 stash 对照确认为既有）
- **运行**（GNOME Wayland/XWayland + vision 对照）：版本列表/分组卡/选择屏与 WPF 参考截图逐项一致；安装→启动端到端两轮成功；重复安装正确拦截
- **资源**：PCL.Avalonia.csproj 新增 forge-installer.jar、java-wrapper.jar 嵌入资源（AvaloniaResourceStore 按 Resources/ 读取）

## 已知限制（后续任务/环境）

- NeoForge/Forge 完整安装受本机网络限制（maven.neoforged.net 不可达；Forge 安装器内部库走官方 maven）；代码管线已验证至 Java 安装器运行
- OptiFine/LiteLoader/LabyMod/LegacyFabric 未移植（MVP 范围外，UI 中不显示）
- Fabric API 下载走 Modrinth CDN 偏慢（2.5 分钟，缓存后正常）
- 「开始下载」按钮为 Highlight 描边样式（任务 5 既定控件视觉语言；WPF 参考图为实心胶囊——后续统一控件批次可加 Accent 实心型）
- 诊断日志（[Diag]）保留为 Debug 级
- **git 提交待完成**：改动已全部暂存，1Password SSH 签名 agent 锁定导致 commit 失败——解锁后 `git commit`（信息已拟好）或告知改用 `--no-gpg-sign`
