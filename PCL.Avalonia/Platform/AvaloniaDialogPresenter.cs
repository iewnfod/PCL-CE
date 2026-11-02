using Avalonia.Threading;
using PCL.Core.UI.Dialogs;

namespace PCL.Avalonia.Platform;

/// <summary>
///     核心层弹窗呈现器的 Avalonia 实现：用 MyMsgBox 呈现 DialogViewModel（文本弹窗 MVP 子集）。
///     按钮回调在关闭后按序执行；界面不可用时降级为提示条。
/// </summary>
public sealed class AvaloniaDialogPresenter : IDialogPresenter
{
    public bool IsAvailable =>
        global::Avalonia.Application.Current?.ApplicationLifetime
            is global::Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime
            {
                MainWindow: { } window
            } && window.WindowState != global::Avalonia.Controls.WindowState.Minimized;

    public void Present(DialogViewModel dialog)
    {
        Dispatcher.UIThread.Post(async () =>
        {
            var labels = new List<string>();
            for (var i = 0; i < dialog.Buttons.Length; i++)
                labels.Add(dialog.Buttons[i].Text ?? (i == 0 ? "确定" : "取消"));
            var choice = await Controls.MyMsgBox.ShowAsync(
                dialog.TextContent, dialog.Title, labels);
            // MyMsgBox 返回 1 基序号；0 取消归到 0
            dialog.Close(new DialogResult(Math.Max(0, choice)));
        });
    }

    public void Dismiss(DialogViewModel dialog)
    {
        // MyMsgBox 无外部强制关闭入口；由用户点击按钮后自然结束
    }

    public void ShowFallback(DialogViewModel dialog)
    {
        Console.WriteLine($"[Dialog/Fallback] {dialog.Title}: {dialog.TextContent}");
        UiBridgeHub.RaiseHint($"[{dialog.Title}] {dialog.TextContent}", NotificationType.Warning);
        dialog.Close(DialogResult.None);
    }
}
