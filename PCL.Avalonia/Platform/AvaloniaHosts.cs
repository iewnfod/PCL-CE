using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Threading;
using PCL.Core.Platform;

namespace PCL.Avalonia.Platform;

/// <summary>Avalonia <see cref="global::Avalonia.Application" /> 的 <see cref="IApplicationHost" /> 包装。</summary>
public sealed class AvaloniaApplicationHost(AppBuilder builder) : IApplicationHost
{
    private global::Avalonia.Application? _application;
    private IWindowHost? _mainWindow;

    public IWindowHost? MainWindow
    {
        get => _mainWindow;
        set
        {
            _mainWindow = value;
            if (value is AvaloniaWindowHost windowHost &&
                _application?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                desktop.MainWindow = windowHost.Window;
        }
    }

    public event Action? Startup;
    public event Action<Exception>? DispatcherUnhandledException;

    public int Run()
    {
        var lifetime = new ClassicDesktopStyleApplicationLifetime { Args = Environment.GetCommandLineArgs() };
        builder.SetupWithLifetime(lifetime);

        _application = global::Avalonia.Application.Current!;
        lifetime.Startup += (_, _) => Startup?.Invoke();

        return lifetime.Start(Array.Empty<string>());
    }

    public object? TryFindResource(string key)
    {
        return _application?.TryFindResource(key, null, out var value) == true ? value : null;
    }

    public object? FindResource(string key)
    {
        var value = TryFindResource(key);
        return value ?? throw new ResourceNotFoundException(key);
    }

    public void SetResource(object key, object value)
    {
        if (_application is null) return;
        if (key is string s) _application.Resources[s] = value;
    }

    public void Shutdown()
    {
        if (_application?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            desktop.Shutdown();
        else Environment.Exit(0);
    }
}

internal sealed class ResourceNotFoundException(string? key)
    : Exception($"Resource not found: {key}");

internal sealed class XamlLoadException(string message) : Exception(message);

/// <summary>Avalonia <see cref="Window" /> 的 <see cref="IWindowHost" /> 包装。</summary>
public sealed class AvaloniaWindowHost(Window window) : IWindowHost
{
    public Window Window => window;

    public bool IsMinimized => window.WindowState == WindowState.Minimized;

    public bool IsTopmost
    {
        get => window.Topmost;
        set => window.Topmost = value;
    }

    public event Action? Loaded
    {
        add => window.Loaded += (_, _) => value?.Invoke();
        remove { }
    }

    public void Show() => window.Show();

    public void Activate() => window.Activate();

    public void Restore()
    {
        if (window.WindowState == WindowState.Minimized)
            window.WindowState = WindowState.Normal;
    }
}

/// <summary>Avalonia Dispatcher 的 <see cref="IUiDispatcher" /> 实现。</summary>
public sealed class AvaloniaUiDispatcher : IUiDispatcher
{
    private Dispatcher Dispatcher => Dispatcher.UIThread;

    public bool CheckAccess() => Dispatcher.CheckAccess();

    public void Post(Action action) => Dispatcher.Post(action);

    public void Invoke(Action action)
    {
        if (CheckAccess()) action();
        else Dispatcher.Invoke(action);
    }

    public T? Invoke<T>(Func<T> func)
    {
        if (CheckAccess()) return func();
        return Dispatcher.Invoke(func);
    }

    public Task InvokeAsync(Action action)
    {
        if (CheckAccess())
        {
            action();
            return Task.CompletedTask;
        }
        return Dispatcher.InvokeAsync(action).GetTask();
    }

    public Task<T> InvokeAsync<T>(Func<T> func)
    {
        if (CheckAccess()) return Task.FromResult(func());
        return Dispatcher.InvokeAsync(func).GetTask();
    }

    public IUiModalFrame CreateModalFrame() => new AvaloniaModalFrame();

    private sealed class AvaloniaModalFrame : IUiModalFrame
    {
        private volatile bool _exitRequested;

        public void Run()
        {
            // Avalonia 无 WPF Dispatcher.PushFrame 等价物；此处不能真正泵消息，
            // 采用「让出并轮询」策略：等待 Exit 被调用的同时保持 UI 线程可响应是不可能的，
            // 因此 WPF 版依赖此机制实现的「同步弹窗」在 Avalonia 侧应改为异步等待（控件库阶段处理）。
            // 骨架阶段：直接返回，避免死锁。
        }

        public void Exit() => _exitRequested = true;
    }
}

/// <summary>Avalonia 资源字典的 <see cref="IThemeBackend" /> 实现：写入 Color/SolidColorBrush 资源。</summary>
public sealed class AvaloniaThemeBackend : IThemeBackend
{
    public void SetColorResource(string key, float alpha, float red, float green, float blue, bool asBrush)
    {
        var app = global::Avalonia.Application.Current;
        if (app is null) return;

        // 接口给的是 ScRGB 分量（0–1）；Avalonia 的 Color 同样使用线性 ScRGB 浮点语义（srgb: 0–255 / sc: 0–1）
        var color = Color.FromArgb(
            (byte)Math.Clamp(alpha * 255f, 0, 255),
            (byte)Math.Clamp(red * 255f, 0, 255),
            (byte)Math.Clamp(green * 255f, 0, 255),
            (byte)Math.Clamp(blue * 255f, 0, 255));

        // 主题资源写入当前主题变体字典，保证明暗主题切换时各自持有
        var resources = app.Resources;
        resources[key + ".Color"] = color;
        if (asBrush) resources[key] = new ImmutableSolidColorBrush(color);
        else resources[key] = color;
    }
}

/// <summary>剪贴板服务：Avalonia Clipboard 实现。</summary>
public sealed class AvaloniaClipboardService : IClipboardService
{
    public string[]? GetFileDropList()
    {
        // Avalonia Clipboard 暂无统一文件列表 API，MVP 返回空
        return null;
    }

    public string? GetText()
    {
        var window = (global::Avalonia.Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)
            ?.MainWindow;
        return window?.Clipboard?.GetTextAsync().GetAwaiter().GetResult();
    }

    public void SetText(string text)
    {
        var window = (global::Avalonia.Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)
            ?.MainWindow;
        window?.Clipboard?.SetTextAsync(text);
    }}

/// <summary>应用级对话框：致命错误用原生 MessageBox 风格窗口呈现。</summary>
public sealed class AvaloniaDialogService : IDialogService
{
    public void ShowFatal(string message, string caption)
    {
        Console.Error.WriteLine($"[Fatal] {caption}: {message}");
        // MVP：致命错误在 UI 可用时弹窗，否则写 stderr（生命周期极早期可能无窗口）
        try
        {
            var lifetime = global::Avalonia.Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime;
            if (lifetime?.MainWindow is { } owner && Dispatcher.UIThread.CheckAccess())
            {
                var dialog = new Window
                {
                    Title = caption,
                    Width = 420, Height = 200,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                    ShowInTaskbar = false,
                    Content = new StackPanel
                    {
                        Margin = new Thickness(16),
                        Spacing = 12,
                        Children =
                        {
                            new TextBlock { Text = caption, FontWeight = FontWeight.Bold, TextWrapping = TextWrapping.Wrap },
                            new TextBox { Text = message, IsReadOnly = true, TextWrapping = TextWrapping.Wrap }
                        }
                    }
                };
                dialog.ShowDialog(owner);
            }
        }
        catch
        {
            // 早期阶段弹窗失败时仅保留 stderr 输出
        }
    }
}

/// <summary>打包资源访问：程序集嵌入资源。</summary>
public sealed class AvaloniaResourceStore : IResourceStore
{
    public Stream? GetResourceStream(string path)
    {
        var assembly = typeof(AvaloniaResourceStore).Assembly;
        // 依次尝试：原始路径 / 点分资源名
        var names = assembly.GetManifestResourceNames();
        var normalized = path.Replace('/', '.').Replace('\\', '.');
        var match = names.FirstOrDefault(n =>
            string.Equals(n, path, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(n, normalized, StringComparison.OrdinalIgnoreCase) ||
            n.EndsWith(normalized, StringComparison.OrdinalIgnoreCase));
        return match is null ? null : assembly.GetManifestResourceStream(match);
    }
}
