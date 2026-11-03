using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Platform.Storage;
using PCL.Avalonia.Controls;
using PCL.Core.App;
using PCL.Core.App.Localization;
using PCL.Core.Minecraft;
using PCL.Core.UI.Hints;

namespace PCL.Avalonia.Pages;

/// <summary>
///     设置-Java 页（复刻 WPF PageSetupJava）：
///     Java 列表（自动选择 + 扫描结果单选、打开文件夹/详情/启停用按钮）+ 手动添加（文件选择器）+ 重新扫描。
/// </summary>
public class PageSetupJava : Grid
{
    private readonly MyCard _cardLoad = new() { Margin = new Thickness(32), IsVisible = false };
    private readonly StackPanel _panContent = new();
    private bool _scanning;

    public PageSetupJava()
    {
        var scroll = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
        };
        var panMain = new StackPanel { Margin = new Thickness(25, 25, 25, 10) };
        scroll.Content = panMain;
        Children.Add(scroll);

        _cardLoad.Content = new StackPanel
        {
            Children =
            {
                new MyLoading { Size = 24 },
                new TextBlock
                {
                    Text = Lang.Text("Setup.Java.Loading"),
                    FontSize = 13,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 8, 0, 0),
                },
            },
        };

        // 添加卡（对齐 WPF：独立紧凑卡片、图标 + 文字的轻量按钮行；无标题卡高 ≈40px）
        var cardAdd = new MyCard { Margin = new Thickness(0, 0, 0, 15) };
        var panAdd = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(20, 7, 20, 7),
        };
        panAdd.Children.Add(_IconTextButton(
            "lucide/folder-plus", Lang.Text("Setup.Java.Add"), 0, 24, _AddJava));
        panAdd.Children.Add(_IconTextButton(
            "lucide/refresh-cw", Lang.Text("Common.Action.Refresh"), 26, 0, () => _ = _RescanAsync()));
        cardAdd.Content = panAdd;
        panMain.Children.Add(cardAdd);

        var card = new MyCard { Margin = new Thickness(0, 0, 0, 15), Title = Lang.Text("Setup.Left.Item.Java") };
        var panBody = new StackPanel { Margin = new Thickness(25, 4, 25, 15) };
        panBody.Children.Add(_cardLoad);
        panBody.Children.Add(_panContent);

        card.Content = panBody;
        panMain.Children.Add(card);

        // 生命周期已自动扫描（JavaService._StartAsync），直接展示列表
        _RefreshList();
    }

    /// <summary>图标 + 文字的轻量按钮行（对齐 WPF MyIconTextButton 视觉：无边框、悬停高亮）。</summary>
    private static Control _IconTextButton(string icon, string text, double marginLeft, double marginRight, Action onClick)
    {
        var btnIcon = new SvgIcon { Icon = icon, Width = 20, Height = 20, VerticalAlignment = VerticalAlignment.Center };
        btnIcon.SetDynamic(SvgIcon.IconBrushProperty, "ColorBrush4");
        var label = new TextBlock
        {
            Text = text,
            FontSize = 13,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(7, 0, 0, 0),
        };
        label.SetDynamic(TextBlock.ForegroundProperty, "ColorBrushMemory");
        var host = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(marginLeft, 0, marginRight, 0),
            Cursor = new Cursor(StandardCursorType.Hand),
            Children =
            {
                btnIcon,
                label,
            },
        };
        host.PointerPressed += (_, e) =>
        {
            e.Handled = true;
            onClick();
        };
        return host;
    }

    /// <summary>重建 Java 列表（对齐 WPF OnLoadFinished）。</summary>
    private void _RefreshList()
    {
        _panContent.Children.Clear();

        // 自动选择项
        var itemAuto = new MyListItem
        {
            Type = MyListItemCheckType.RadioBox,
            Title = Lang.Text("Setup.Java.AutoSelect.Title"),
            Info = Lang.Text("Setup.Java.AutoSelect.Info"),
        };
        itemAuto.Check += (_, _) => Config.Launch.SelectedJava = "";
        _panContent.Children.Add(itemAuto);

        var current = Config.Launch.SelectedJava;
        foreach (var entry in JavaRuntime.Javas.GetSortedJavaList())
        {
            var item = _BuildItem(entry);
            _panContent.Children.Add(item);
            if (entry.Installation.JavaExePath == current)
                item.SetChecked(true, false, false);
        }

        if (string.IsNullOrEmpty(current))
            itemAuto.SetChecked(true, false, false);
    }

    private MyListItem _BuildItem(JavaEntry entry)
    {
        var item = new MyListItem
        {
            Type = MyListItemCheckType.RadioBox,
            Title = $"{(entry.Installation.IsJre ? "JRE" : "JDK")} {entry.Installation.MajorVersion}",
            Info = entry.Installation.JavaFolder,
            PlainTags = [entry.Installation.Is64Bit ? "64 Bit" : "32 Bit", entry.Installation.Brand.ToString()],
        };
        item.Check += (_, e) =>
        {
            // 对齐 WPF TrySelectJava：不可用/未启用的 Java 不允许选择
            if (!entry.Installation.IsStillAvailable)
            {
                HintService.Show(new() { Message = Lang.Text("Setup.Java.Unavailable"), Kind = HintKind.Info });
                e.Handled = true;
                return;
            }
            if (!entry.IsEnabled)
            {
                HintService.Show(new() { Message = Lang.Text("Setup.Java.EnableBeforeSelect"), Kind = HintKind.Info });
                e.Handled = true;
                return;
            }
            Config.Launch.SelectedJava = entry.Installation.JavaExePath;
        };

        var btnOpen = new MyIconButton { SvgIcon = "lucide/folder-open" };
        ToolTip.SetTip(btnOpen, Lang.Text("Common.Action.Open"));
        btnOpen.Click += (_, _) =>
        {
            if (!entry.Installation.IsStillAvailable) return;
            try
            {
                if (OperatingSystem.IsWindows())
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(entry.Installation.JavaFolder) { UseShellExecute = true });
                else
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(
                        "xdg-open", $"\"{entry.Installation.JavaFolder}\"") { UseShellExecute = false });
            }
            catch (Exception ex)
            {
                HintService.Show(new() { Message = ex.Message, Kind = HintKind.Error });
            }
        };

        var btnInfo = new MyIconButton { SvgIcon = "lucide/info" };
        ToolTip.SetTip(btnInfo, Lang.Text("Setup.Java.Detail.ToolTip"));
        btnInfo.Click += (_, _) =>
        {
            if (!entry.Installation.IsStillAvailable) return;
            var install = entry.Installation;
            _ = Controls.MyMsgBox.ShowAsync(
                Lang.Text("Setup.Java.Info.Format",
                    install.IsJre ? "JRE" : "JDK",
                    install.Version.ToString(),
                    install.Architecture.ToString(),
                    install.Is64Bit ? "64 Bit" : "32 Bit",
                    install.Brand.ToString(),
                    install.JavaFolder),
                Lang.Text("Setup.Java.Info.Title"));
        };

        var btnToggle = new MyIconButton
        {
            SvgIcon = entry.IsEnabled ? "lucide/circle-minus" : "lucide/circle-check",
        };
        ToolTip.SetTip(btnToggle, entry.IsEnabled ? Lang.Text("Setup.Java.Disable") : Lang.Text("Setup.Java.Enable"));
        btnToggle.Click += (_, _) =>
        {
            try
            {
                var target = JavaRuntime.Javas.AddOrGet(entry.Installation.JavaExePath);
                if (target is null)
                {
                    HintService.Show(new() { Message = Lang.Text("Setup.Java.Unavailable"), Kind = HintKind.Info });
                    return;
                }
                if (target.IsEnabled && Config.Launch.SelectedJava == target.Installation.JavaExePath)
                {
                    HintService.Show(new() { Message = Lang.Text("Setup.Java.DeselectBeforeDisable"), Kind = HintKind.Info });
                    return;
                }
                target.IsEnabled = !target.IsEnabled;
                JavaRuntime.Javas.SaveConfig();
                _RefreshList();
            }
            catch (Exception ex)
            {
                HintService.Show(new() { Message = Lang.Text("Setup.Java.EnableFailed"), Kind = HintKind.Error });
                Console.WriteLine($"[Setup.Java] 启停用失败：{ex}");
            }
        };

        item.Buttons = [btnOpen, btnInfo, btnToggle];
        return item;
    }

    /// <summary>手动添加 Java（StorageProvider 文件选择器，对齐 WPF BtnAdd_Click）。</summary>
    private async void _AddJava()
    {
        try
        {
            var top = VisualRoot as TopLevel;
            if (top is null) return;
            var files = await top.StorageProvider.OpenFilePickerAsync(new()
            {
                Title = Lang.Text("Setup.Java.SelectFile.Title"),
                AllowMultiple = false,
                FileTypeFilter =
                [
                    new("Java") { Patterns = ["java", "java.exe", "javaw", "javaw.exe"] },
                    new("All") { Patterns = ["*"] },
                ],
            });
            var path = files.Count == 1 ? files[0].TryGetLocalPath() : null;
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return;

            if (JavaRuntime.Javas.Exist(path))
            {
                HintService.Show(new() { Message = Lang.Text("Setup.Java.AlreadyExists"), Kind = HintKind.Info });
                return;
            }
            var added = await Task.Run(() =>
            {
                var entry = JavaRuntime.Javas.AddOrGet(path);
                JavaRuntime.Javas.SaveConfig();
                return entry;
            });
            if (added is not null)
            {
                HintService.Show(new() { Message = Lang.Text("Setup.Java.Added"), Kind = HintKind.Success });
                _RefreshList();
            }
            else
            {
                HintService.Show(new() { Message = Lang.Text("Setup.Java.AddFailed"), Kind = HintKind.Error });
            }
        }
        catch (Exception ex)
        {
            HintService.Show(new() { Message = Lang.Text("Setup.Java.AddFailed"), Kind = HintKind.Error });
            Console.WriteLine($"[Setup.Java] 添加失败：{ex}");
        }
    }

    /// <summary>重新扫描（强制，对齐 WPF ScanThenRefreshAsync）。</summary>
    private async Task _RescanAsync()
    {
        if (_scanning) return;
        _scanning = true;
        _cardLoad.IsVisible = true;
        _panContent.IsVisible = false;
        try
        {
            await Task.Run(async () =>
            {
                await JavaService.JavaManager.ScanJavaAsync(force: true);
                JavaService.JavaManager.CheckAllAvailability();
            });
            _RefreshList();
        }
        finally
        {
            _scanning = false;
            _cardLoad.IsVisible = false;
            _panContent.IsVisible = true;
        }
    }
}
