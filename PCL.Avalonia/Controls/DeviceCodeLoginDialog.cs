using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Shapes;
using Avalonia.Layout;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Threading;
using PCL.Core.App.Localization;
using PCL.Core.Logging;
using PCL.Core.Minecraft.IdentityModel;
using PCL.Core.Minecraft.IdentityModel.OAuth;
using PCL.Core.Minecraft.Profile.Authentication;

namespace PCL.Avalonia.Controls;

/// <summary>
///     设备码登录弹窗（复刻 WPF MyMsgLogin）：说明文案（含授权码）+ 用户码大字展示 +
///     「重新打开网页 / 复制代码 / 取消」三按钮；显示 2 秒后自动打开浏览器并复制授权码，
///     后台按 RFC 8628 轮询令牌端点直至成功 / 过期 / 拒绝 / 用户取消。
/// </summary>
public static class DeviceCodeLoginDialog
{
    /// <summary>
    ///     显示弹窗并等待设备码授权结果。用户取消或授权失败时返回 null（异常仅记日志）。
    /// </summary>
    public static Task<AuthorizeResult?> ShowAsync(DeviceCodeAuthenticationContext context, CancellationToken token)
    {
        var tcs = new TaskCompletionSource<AuthorizeResult?>(TaskCreationOptions.RunContinuationsAsynchronously);
        Dispatcher.UIThread.Post(() => _Show(context, token, tcs));
        return tcs.Task;
    }

    private static void _Show(DeviceCodeAuthenticationContext context, CancellationToken token,
        TaskCompletionSource<AuthorizeResult?> tcs)
    {
        try
        {
            var owner = (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)
                ?.MainWindow;
            if (owner?.Content is not Panel host)
            {
                Console.WriteLine("[DeviceCodeLogin] 无宿主窗口，降级为控制台输出");
                tcs.TrySetResult(null);
                return;
            }

            var device = context.Data;
            var userCode = device.UserCode ?? "";
            var website = !string.IsNullOrEmpty(device.VerificationUriComplete)
                ? device.VerificationUriComplete!
                : device.VerificationUri ?? "https://www.microsoft.com/link";
            var caption = !string.IsNullOrEmpty(device.VerificationUriComplete)
                ? Lang.Text("Launch.Account.LoginDialog.MicrosoftInstructions.WithAutoFill", userCode, website)
                : Lang.Text("Launch.Account.LoginDialog.MicrosoftInstructions", userCode, website);

            var finished = false;
            var completionLock = new Lock();
            var uiCts = CancellationTokenSource.CreateLinkedTokenSource(token);
            Border? overlayRef = null;

            void Finish(AuthorizeResult? result)
            {
                lock (completionLock)
                {
                    if (finished) return;
                    finished = true;
                }

                uiCts.Cancel();
                Dispatcher.UIThread.Post(() =>
                {
                    if (overlayRef is { } ov)
                    {
                        ov.IsEnabled = false;
                        host.Children.Remove(ov);
                    }
                });
                tcs.TrySetResult(result);
            }

            // ===== 弹窗结构 =====
            var title = new TextBlock
            {
                Text = Lang.Text("Launch.Account.LoginDialog.MinecraftLogin"),
                FontSize = 20,
                FontWeight = FontWeight.Bold,
            };
            title.Bind(TextBlock.ForegroundProperty,
                new DynamicResourceExtension("ColorBrush2"));

            var codeBlock = new TextBlock
            {
                Text = userCode,
                FontSize = 26,
                FontWeight = FontWeight.Bold,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 14, 0, 4),
                FontFamily = new FontFamily("Mono,Menlo,Consolas,monospace"),
            };
            codeBlock.Bind(TextBlock.ForegroundProperty,
                new DynamicResourceExtension("ColorBrush2"));

            var captionBlock = new TextBlock
            {
                Text = caption,
                FontSize = 13.5,
                TextWrapping = TextWrapping.Wrap,
                MaxWidth = 420,
                LineHeight = 19,
            };

            MyButton Btn(string text, bool primary = false)
            {
                return new MyButton
                {
                    Text = text,
                    Height = 30,
                    MinWidth = 92,
                    Margin = new Thickness(8, 0, 0, 0),
                    ColorType = primary ? MyButtonColorType.Highlight : MyButtonColorType.Normal,
                };
            }

            var btnReopen = Btn(Lang.Text("Launch.Account.LoginDialog.ReopenWebpage"));
            var btnCopy = Btn(Lang.Text("Launch.Account.LoginDialog.CopyCode"));
            var btnCancel = Btn(Lang.Text("Common.Action.Cancel"));

            var line = new Rectangle { Height = 2, Margin = new Thickness(0, 10, 0, 14) };
            line.Bind(Rectangle.FillProperty,
                new DynamicResourceExtension("ColorBrush2"));

            var dialog = new Border
            {
                CornerRadius = new CornerRadius(7),
                Padding = new Thickness(22, 20, 22, 20),
                MinWidth = 400,
                MaxWidth = 480,
                BoxShadow = BoxShadows.Parse("0 8 24 0 #66000000"),
                Child = new StackPanel
                {
                    Children =
                    {
                        title, line, codeBlock, captionBlock,
                        new StackPanel
                        {
                            Orientation = Orientation.Horizontal,
                            HorizontalAlignment = HorizontalAlignment.Right,
                            Margin = new Thickness(0, 16, 0, 0),
                            Children = { btnReopen, btnCopy, btnCancel },
                        },
                    },
                },
            };
            var app = Application.Current;
            if (app?.Resources.TryGetResource("ColorBrushBackground", app.ActualThemeVariant, out var bg) == true &&
                bg is IBrush brush)
                dialog.Background = brush;
            else
                dialog.Background = Brushes.White;

            var overlay = new Border
            {
                Background = new ImmutableSolidColorBrush(Color.FromUInt32(0x66000000)),
                ZIndex = 2000,
                Child = new Panel
                {
                    Children =
                    {
                        dialog,
                    },
                },
            };
            dialog.HorizontalAlignment = HorizontalAlignment.Center;
            dialog.VerticalAlignment = VerticalAlignment.Center;

            // ===== 交互 =====
            void OpenWebsite() => PCL.LauncherUi.Interaction?.OpenWebsite(website);
            void CopyCode() => _ = owner.Clipboard?.SetTextAsync(userCode);

            btnReopen.Click += (_, _) => OpenWebsite();
            btnCopy.Click += (_, _) => CopyCode();
            btnCancel.Click += (_, _) => Finish(null);

            overlayRef = overlay;
            host.Children.Add(overlay);

            LogWrapper.Info("Profile", $"设备码登录弹窗：{userCode} → {website}");
            OpenWebsite();
            CopyCode();

            // ===== 轮询（复刻 MyMsgLogin.WorkThreadAsync）=====
            _ = Task.Run(async () =>
            {
                var pollToken = uiCts.Token;
                var delayTime = TimeSpan.FromSeconds(Math.Max(2, device.Interval ?? 5));
                var unknownFailureCount = 0;
                while (!pollToken.IsCancellationRequested)
                    try
                    {
                        var result = await context.PollAsync(pollToken).ConfigureAwait(false);
                        pollToken.ThrowIfCancellationRequested();
                        if (result is { IsError: true })
                            switch (result.Error)
                            {
                                case "authorization_pending":
                                    await Task.Delay(delayTime, pollToken).ConfigureAwait(false);
                                    continue;
                                case "slow_down":
                                    delayTime += TimeSpan.FromSeconds(5);
                                    await Task.Delay(delayTime, pollToken).ConfigureAwait(false);
                                    continue;
                                case "expired_token":
                                    LogWrapper.Warn("Profile", "设备授权已过期");
                                    Finish(null);
                                    return;
                                case "access_denied":
                                case "authorization_declined":
                                    LogWrapper.Warn("Profile", "用户拒绝了设备授权");
                                    Finish(null);
                                    return;
                                default:
                                    throw new IdentityModelAuthenticationException(
                                        result.Error ?? "invalid_response",
                                        result.ErrorDescription ?? "Unable to get body");
                            }

                        if (result is null) throw new InvalidDataException("微软令牌返回为空");
                        LogWrapper.Info("Profile", $"令牌过期时间：{result.ExpiresIn} 秒");
                        Finish(result);
                        return;
                    }
                    catch (OperationCanceledException)
                    {
                        Finish(null);
                        return;
                    }
                    catch (Exception ex)
                    {
                        if (unknownFailureCount < 2)
                        {
                            unknownFailureCount++;
                            LogWrapper.Warn("Profile", $"正版验证轮询第 {unknownFailureCount} 次失败：{ex.Message}");
                            try { await Task.Delay(2000, pollToken).ConfigureAwait(false); }
                            catch (OperationCanceledException) { Finish(null); return; }
                        }
                        else
                        {
                            LogWrapper.Warn("Profile", $"正版验证轮询失败（放弃）：{ex}");
                            Finish(null);
                            return;
                        }
                    }
            }, uiCts.Token);
        }
        catch (Exception ex)
        {
            LogWrapper.Error(ex, "Profile", "设备码登录弹窗初始化失败");
            tcs.TrySetResult(null);
        }
    }
}
