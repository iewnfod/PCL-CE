namespace PCL.Avalonia.Controls;

/// <summary>
///     路由事件参数（对齐 WPF 主程序 Common.RouteEventArgs）：User 标记是否由用户交互引发，
///     Handled 置 true 可取消勾选变更。
/// </summary>
public class RouteEventArgs(bool user = false) : EventArgs
{
    public bool User { get; } = user;
    public bool Handled { get; set; }
}
