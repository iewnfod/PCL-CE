using Avalonia;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using PCL.Avalonia.Controls;
using PCL.Core.App.Localization;

namespace PCL.Avalonia.Pages;

/// <summary>
///     实例选择左侧页（复刻 WPF PageSelectLeft）：Minecraft 文件夹列表。<br/>
///     标题（12px 60%）+ 文件夹单选列表（高 40、Info=路径、右侧设置按钮）+「添加或导入」区（高 34 项）。
/// </summary>
public class PageSelectLeft : Grid
{
    private readonly StackPanel _panList = new();
    private List<GameFolders.McFolder>? _folderListLast;

    public PageSelectLeft()
    {
        var scroll = new ScrollViewer
        {
            Content = _panList,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            MinWidth = 250,
            MaxWidth = 300,
            VerticalAlignment = VerticalAlignment.Top,
        };
        Children.Add(scroll);

        GameFolders.McFolderListLoader.PreviewFinish += _ =>
            Dispatcher.UIThread.Post(RefreshFolderList);
        RefreshFolderList();
    }

    /// <summary>重建文件夹列表 UI（数据变化时；对齐 WPF McFolderListUI）。</summary>
    public void RefreshFolderList()
    {
        try
        {
            if (_folderListLast is not null && _folderListLast.SequenceEqual(GameFolders.mcFolderList))
                return;
            _folderListLast = [.. GameFolders.mcFolderList];

            _panList.Children.Clear();

            // 文件夹列表标题
            _panList.Children.Add(new TextBlock
            {
                Text = Lang.Text("Select.Folder.ListTitle"),
                Margin = new Thickness(13, 18, 5, 4),
                Opacity = 0.6,
                FontSize = 12,
            });

            for (var i = 0; i < GameFolders.mcFolderList.Count; i++)
            {
                var folder = GameFolders.mcFolderList[i];

                var item = new MyListItem
                {
                    IsScaleAnimationEnabled = false,
                    Type = MyListItemCheckType.RadioBox,
                    MinPaddingRight = 30,
                    Title = folder.DisplayName,
                    Info = folder.Location,
                    Height = 40,
                    Tag = folder,
                };
                var index = i;
                item.Changed += (sender, _) => _FolderChange(sender, index);
                // 右侧设置按钮（WPF：弹出右键菜单；MVP：打开文件夹）
                var btnSettings = new MyIconButton
                {
                    SvgIcon = "lucide/settings",
                    LogoScale = 1.1,
                };
                btnSettings.Click += (_, _) =>
                {
                    try
                    {
                        PCL.Core.App.Basics.OpenPath(folder.Location);
                    }
                    catch (Exception ex)
                    {
                        global::PCL.Core.Logging.LogWrapper.Warn("Select", $"打开文件夹失败：{ex.Message}");
                    }
                };
                item.Buttons = [btnSettings];
                _panList.Children.Add(item);
            }

            // 添加或导入标题
            _panList.Children.Add(new TextBlock
            {
                Text = Lang.Text("Select.Folder.AddOrImport"),
                Margin = new Thickness(13, 18, 5, 4),
                Opacity = 0.6,
                FontSize = 12,
            });

            // 创建新文件夹（仅当默认 .minecraft 不存在时显示，对照 WPF）/ 添加已有文件夹 / 导入整合包
            void AddAction(string titleKey, string toolTipKey, string svgIcon, Action? click = null)
            {
                var action = new MyListItem
                {
                    IsScaleAnimationEnabled = false,
                    Type = MyListItemCheckType.Clickable,
                    Title = Lang.Text(titleKey),
                    Height = 34,
                    SvgIcon = svgIcon,
                    LogoScale = 0.9,
                };
                ToolTip.SetTip(action, Lang.Text(toolTipKey));
                action.Click += (_, _) => click?.Invoke();
                _panList.Children.Add(action);
            }

            var defaultFolder = System.IO.Path.Combine(
                PCL.Core.App.Basics.ExecutableDirectory, ".minecraft");
            if (!Directory.Exists(defaultFolder))
                AddAction("Select.Folder.CreateNew.Title", "Select.Folder.CreateNew.ToolTip",
                    "lucide/folder-plus", _CreateFolder);
            AddAction("Select.Folder.AddExisting.Title", "Select.Folder.AddExisting.ToolTip",
                "lucide/folder-input", _AddExistingFolder);
            AddAction("Select.Folder.ImportModpack.Title", "Select.Folder.ImportModpack.ToolTip",
                "lucide/package-plus", () =>
                    global::PCL.Core.UI.Hints.HintService.Show(new()
                    {
                        Message = "导入整合包将在后续版本提供（MVP 占位）",
                        Kind = global::PCL.Core.UI.Hints.HintKind.Info,
                    }));

            // 底部边距
            _panList.Children.Add(new Control { Height = 10 });

            // 确认勾选状态
            for (var i = 0; i < GameFolders.mcFolderList.Count; i++)
            {
                if (GameFolders.mcFolderList[i].Location == GameFolders.mcFolderSelected &&
                    _panList.Children[i + 1] is MyListItem listItem)
                {
                    listItem.Checked = true;
                    return;
                }
            }

            if (GameFolders.mcFolderList.Count > 0 &&
                _panList.Children[1] is MyListItem first)
                first.Checked = true;
        }
        catch (Exception ex)
        {
            global::PCL.Core.Logging.LogWrapper.Error(ex, "构建 Minecraft 文件夹列表 UI 出错");
        }
    }

    /// <summary>新建默认 .minecraft 文件夹（对齐 WPF CreateFolderCommand，路径跨平台化）。</summary>
    private void _CreateFolder()
    {
        try
        {
            var root = System.IO.Path.Combine(
                PCL.Core.App.Basics.ExecutableDirectory, ".minecraft");
            if (!Directory.Exists(root))
            {
                Directory.CreateDirectory(System.IO.Path.Combine(root, "versions"));
                GameFolders.mcFolderSelected = root;
                GameFolders.McFolderLauncherProfilesJsonCreate(root
                    + System.IO.Path.DirectorySeparatorChar);
                global::PCL.Core.UI.Hints.HintService.Show(new()
                {
                    Message = Lang.Text("Select.Folder.CreateSuccess"),
                    Kind = global::PCL.Core.UI.Hints.HintKind.Success,
                });
            }

            GameFolders.McFolderListLoader.Start(isForceRestart: true);
        }
        catch (Exception ex)
        {
            global::PCL.Core.Logging.LogWrapper.Error(ex, "新建 Minecraft 文件夹失败");
            global::PCL.Core.UI.Hints.HintService.Show(new()
            {
                Message = $"新建文件夹失败：{ex.Message}",
                Kind = global::PCL.Core.UI.Hints.HintKind.Error,
            });
        }
    }

    /// <summary>添加已有文件夹（Avalonia StorageProvider 选择器 + 对齐 WPF AddFolder 核心逻辑）。</summary>
    private async void _AddExistingFolder()
    {
        try
        {
            var top = VisualRoot as global::Avalonia.Controls.TopLevel;
            if (top is null) return;
            var folder = await top.StorageProvider.OpenFolderPickerAsync(new()
            {
                Title = Lang.Text("Select.Folder.AddExisting.ToolTip"),
                AllowMultiple = false,
            });
            var path = folder.Count == 1 ? folder[0].TryGetLocalPath() : null;
            if (string.IsNullOrEmpty(path)) return;
            if (path.Contains('!') || path.Contains(';'))
            {
                global::PCL.Core.UI.Hints.HintService.Show(new()
                {
                    Message = Lang.Text("Select.Folder.InvalidPathChars"),
                    Kind = global::PCL.Core.UI.Hints.HintKind.Error,
                });
                return;
            }

            // 校验：目录本身或其子目录含 versions（对齐 WPF 的自动探测）
            if (!Directory.Exists(System.IO.Path.Combine(path, "versions")))
            {
                var probed = Directory.GetDirectories(path)
                    .FirstOrDefault(d => Directory.Exists(System.IO.Path.Combine(d, "versions")));
                if (probed is not null) path = probed;
            }

            var separator = System.IO.Path.DirectorySeparatorChar;
            var normPath = path.TrimEnd(separator) + separator;

            // 默认显示名：目录名（.minecraft 取上级）
            var parts = normPath.TrimEnd(separator).Split(separator);
            var last = parts.Length > 0 ? parts[^1] : "";
            var defaultName = last == ".minecraft"
                ? (parts.Length >= 2 ? parts[^2] : "Minecraft")
                : (last.Length > 0 ? last : "Minecraft");
            if (defaultName.Length > 40) defaultName = defaultName[..39];

            // 查重（已在列表则直接选中）
            foreach (var existing in GameFolders.mcFolderList)
                if (existing.Location == normPath)
                {
                    global::PCL.Core.UI.Hints.HintService.Show(new()
                    {
                        Message = Lang.Text("Select.Folder.AlreadyInList"),
                        Kind = global::PCL.Core.UI.Hints.HintKind.Info,
                    });
                    return;
                }

            // 写入配置并重启文件夹加载（WPF AddFolder 同款持久化格式）
            var folders = string.IsNullOrEmpty(PCL.Core.App.States.Game.Folders)
                ? []
                : PCL.Core.App.States.Game.Folders.Split("|").ToList();
            folders.Add($"{defaultName}>{normPath}");
            PCL.Core.App.States.Game.Folders = string.Join("|", folders);
            PCL.Core.App.States.Game.SelectedFolder = normPath;
            GameFolders.mcFolderSelected = normPath;
            GameFolders.McFolderListLoader.Start(isForceRestart: true);
            global::PCL.Core.UI.Hints.HintService.Show(new()
            {
                Message = Lang.Text("Select.Folder.Added", defaultName),
                Kind = global::PCL.Core.UI.Hints.HintKind.Success,
            });
        }
        catch (Exception ex)
        {
            global::PCL.Core.Logging.LogWrapper.Error(ex, "添加 Minecraft 文件夹失败");
        }
    }

    /// <summary>切换选中文件夹（对齐 WPF Folder_Change：更新选择并刷新实例列表）。</summary>
    private void _FolderChange(object? sender, int index)    {
        if (sender is not MyListItem { Checked: true } item) return;
        if (index < 0 || index >= GameFolders.mcFolderList.Count) return;
        var folder = GameFolders.mcFolderList[index];
        if (GameFolders.mcFolderSelected == folder.Location) return;

        GameFolders.mcFolderSelected = folder.Location;
        global::PCL.Core.Logging.LogWrapper.Info("[Minecraft] 切换 Minecraft 文件夹：" + folder.DisplayName);
        // 触发实例列表加载（右页监听同一 loader 刷新）
        Loading.LoaderFolderRun(InstanceList.mcInstanceListLoader, GameFolders.mcFolderSelected,
            Loading.LoaderFolderRunType.RunOnUpdated, 1, "versions" + System.IO.Path.DirectorySeparatorChar);
    }
}
