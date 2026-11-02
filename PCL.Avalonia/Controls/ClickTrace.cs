using System.Text;

namespace PCL.Avalonia.Controls;

/// <summary>
///     点击事件流追踪（临时诊断）：PCL_TRACE_CLICK=1 时把控件 Pointer 事件写入 /tmp/opencode/pcl-click.log。
/// </summary>
public static class ClickTrace
{
    private static readonly object Gate = new();
    private static readonly StringBuilder Buffer = new();

    public static bool Enabled => Environment.GetEnvironmentVariable("PCL_TRACE_CLICK") == "1";

    public static void Log(string message)
    {
        if (!Enabled) return;
        lock (Gate)
        {
            Buffer.AppendLine($"[{DateTime.Now:HH:mm:ss.fff}] {message}");
            try
            {
                File.WriteAllText("/tmp/opencode/pcl-click.log", Buffer.ToString());
            }
            catch
            {
                // 诊断失败静默
            }
        }
    }
}
