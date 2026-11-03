using System.IO;
using System.Runtime.InteropServices;
using Avalonia;
using System.Text.Json.Nodes;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using PCL.Core.Logging;
using PCL.Network;

namespace PCL.Avalonia.Platform;

/// <summary>
///     皮肤获取与头像合成（Avalonia 版，逻辑复刻 WPF Modules/Minecraft/Skin/Skin.cs）：
///     sessionserver 查询皮肤地址（INI 缓存）→ 下载皮肤 PNG → 裁「脸 + 头发」两层合成 56×56 头像。
/// </summary>
public static class McSkinHelper
{
    private static readonly Lock DownloadLock = new();

    /// <summary>获取 UUID 对应的皮肤文件地址（带 INI 缓存），失败抛异常。复刻 Skin.McSkinGetAddress("Ms")。</summary>
    public static string GetSkinAddress(string uuid)
    {
        if (string.IsNullOrEmpty(uuid))
            throw new ArgumentException("UUID 为空", nameof(uuid));

        var cachePath = Path.Combine(LauncherPaths.Temp, "Cache", "Skin", "IndexMs.ini");
        var cached = LauncherCommon.ReadIni(cachePath, uuid);
        if (!string.IsNullOrEmpty(cached)) return cached;

        var url = "https://sessionserver.mojang.com/session/minecraft/profile/" + uuid;
        var skinString = NetFacade.NetGetCodeByRequestRetry(url) as string;
        if (string.IsNullOrEmpty(skinString))
            throw new InvalidDataException("皮肤会话服务器返回为空");

        string? skinValue = null;
        try
        {
            var json = (JsonObject)LauncherCommon.GetJson(skinString);
            if (json["properties"]?.AsArray() is { } properties)
                foreach (var property in properties.OfType<JsonObject>())
                    if (property["name"]?.ToString() == "textures")
                    {
                        skinValue = property["value"]?.ToString();
                        break;
                    }
        }
        catch (Exception ex)
        {
            throw new InvalidDataException("无法解析皮肤返回值（可能是未设置自定义皮肤的用户）", ex);
        }

        if (skinValue is null)
            throw new InvalidDataException("皮肤返回值中未找到 textures 属性");

        var decoded = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(skinValue));
        var skinJson = (JsonObject)LauncherCommon.GetJson(decoded.ToLowerInvariant());
        var skinUrlNode = skinJson["textures"]?["skin"]?["url"];
        if (skinUrlNode is null)
            throw new InvalidDataException("用户未设置自定义皮肤");

        var skinUrl = skinUrlNode.ToString();
        if (skinUrl.Contains("minecraft.net/"))
            skinUrl = skinUrl.Replace("http://", "https://");

        Directory.CreateDirectory(Path.GetDirectoryName(cachePath)!);
        LauncherCommon.WriteIni(cachePath, uuid, skinUrl);
        LogWrapper.Info("Skin", $"UUID {uuid} 对应的皮肤文件为 {skinUrl}");
        return skinUrl;
    }

    /// <summary>下载皮肤到本地缓存（复刻 Skin.McSkinDownload），返回本地文件路径。</summary>
    public static string DownloadSkin(string address)
    {
        var fileAddress = Path.Combine(LauncherPaths.Temp, "Cache", "Skin",
            LauncherCommon.GetHash(address) + ".png");
        lock (DownloadLock)
        {
            if (!File.Exists(fileAddress))
            {
                FileDownloader.DownloadAsync(address, fileAddress).GetAwaiter().GetResult();
                LogWrapper.Info("Skin", "皮肤下载成功：" + fileAddress);
            }

            return fileAddress;
        }
    }

    /// <summary>
    ///     由皮肤 PNG 合成 56×56 头像（脸层 (8,8,8,8) 放大至 (4,4,48,48) + 头发层 (40,8,8,8) 覆盖整幅，最近邻），
    ///     写入 Cache/Skin/Head/{id}.png 后返回路径；皮肤已缓存时直接返回。对应 WPF MySkin 的头像导出。
    /// </summary>
    public static string EnsureAvatar(string skinFile)
    {
        var id = Path.GetFileNameWithoutExtension(skinFile);
        var dir = Path.Combine(LauncherPaths.Temp, "Cache", "Skin", "Head");
        var cachePath = Path.Combine(dir, id + ".png");
        if (File.Exists(cachePath) && new FileInfo(cachePath).Length > 0) return cachePath;

        using var skin = new Bitmap(skinFile);
        var w = skin.PixelSize.Width;
        var h = skin.PixelSize.Height;
        if (w is not (64 or 128) || h < 32)
            throw new InvalidDataException($"皮肤尺寸不标准：{w}x{h}");

        var scale = w / 64;

        // 读取皮肤像素（与输出同为 Skia BGRA 预乘格式）
        var srcStride = w * 4;
        var src = new byte[srcStride * h];
        var srcPtr = Marshal.AllocHGlobal(src.Length);
        try
        {
            skin.CopyPixels(new PixelRect(0, 0, w, h), srcPtr, src.Length, srcStride);
            Marshal.Copy(srcPtr, src, 0, src.Length);
        }
        finally
        {
            Marshal.FreeHGlobal(srcPtr);
        }

        byte[] Px(int x, int y) => [src[(y * srcStride) + (x * 4)], src[(y * srcStride) + (x * 4) + 1],
            src[(y * srcStride) + (x * 4) + 2], src[(y * srcStride) + (x * 4) + 3]];

        // 合成 56×56：脸 8×8 → (4,4) 起 48×48；头发 8×8 → 整幅 56×56（预乘 alpha：dst = src + dst×(1-srcA)）
        const int outSize = 56;
        var output = new byte[outSize * outSize * 4];
        for (var y = 0; y < outSize; y++)
        for (var x = 0; x < outSize; x++)
        {
            // 脸层（下层）
            int b = 0, g = 0, r = 0, a = 0;
            if (x >= 4 && x < 52 && y >= 4 && y < 52)
            {
                var face = Px(scale * (8 + (x - 4) * 8 / 48), scale * (8 + (y - 4) * 8 / 48));
                b = face[0]; g = face[1]; r = face[2]; a = face[3];
            }

            // 头发层（上层，仅 64+ 宽皮肤含附加层区域）
            if (w >= 64)
            {
                var hair = Px(scale * (40 + x * 8 / outSize), scale * (8 + y * 8 / outSize));
                var ha = hair[3];
                if (ha > 0)
                {
                    b = hair[0] + (b * (255 - ha) / 255);
                    g = hair[1] + (g * (255 - ha) / 255);
                    r = hair[2] + (r * (255 - ha) / 255);
                    a = ha + (a * (255 - ha) / 255);
                }
            }

            var offset = (y * outSize + x) * 4;
            output[offset] = (byte)Math.Clamp(b, 0, 255);
            output[offset + 1] = (byte)Math.Clamp(g, 0, 255);
            output[offset + 2] = (byte)Math.Clamp(r, 0, 255);
            output[offset + 3] = (byte)Math.Clamp(a, 0, 255);
        }

        Directory.CreateDirectory(dir);
        using (var avatar = new WriteableBitmap(new PixelSize(outSize, outSize),
                   new Vector(96, 96)))
        {
            using (var fb = avatar.Lock())
            {
                var rowPtr = fb.Address;
                for (var row = 0; row < outSize; row++)
                {
                    Marshal.Copy(output, row * outSize * 4, rowPtr, outSize * 4);
                    rowPtr += fb.RowBytes;
                }
            }

            using var fs = File.Create(cachePath);
            avatar.Save(fs);
        }

        return cachePath;
    }

    /// <summary>获取档案头像路径：优先皮肤缓存（SkinHeadId），无则返回 null（调用方降级为 lucide/user 图标）。</summary>
    public static string? TryGetAvatarPath(PCL.Core.Minecraft.Profile.Models.McProfile profile)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(profile.SkinHeadId))
            {
                var head = Path.Combine(LauncherPaths.Temp, "Cache", "Skin", "Head",
                    profile.SkinHeadId + ".png");
                if (File.Exists(head) && new FileInfo(head).Length > 0) return head;
            }

            // 未缓存：在线拉取并合成（离线档案跳过）
            if (profile.ProfileType == PCL.Core.Minecraft.Profile.Models.ProfileType.Offline ||
                string.IsNullOrWhiteSpace(profile.Uuid) || profile.Uuid.StartsWith("00000"))
                return null;

            var skinFile = DownloadSkin(GetSkinAddress(profile.Uuid));
            var avatar = EnsureAvatar(skinFile);

            // 回写 SkinHeadId（与 WPF MySkin 一致），失败不影响显示
            var current = PCL.Core.Minecraft.Profile.ProfileService.Profiles
                .FirstOrDefault(p => p.ProfileId == profile.ProfileId);
            if (current is not null && current.SkinHeadId != Path.GetFileNameWithoutExtension(skinFile))
            {
                var updated = current.Clone();
                updated.SkinHeadId = Path.GetFileNameWithoutExtension(skinFile);
                PCL.Core.Minecraft.Profile.ProfileService.Update(current, updated);
                PCL.Core.Minecraft.Profile.ProfileService.Select(updated);
            }

            return avatar;
        }
        catch (Exception ex)
        {
            LogWrapper.Warn("Skin", $"获取档案头像失败（{profile.UserName}）：{ex.Message}");
            return null;
        }
    }
}
