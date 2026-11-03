using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Text.Json.Nodes;
using PCL.Core.App;
using PCL.Core.App.Localization;
using PCL.Core.Minecraft;
using PCL.Core.Utils;
using PCL.Network;
using PCL.Network.Loaders;
using PCL.Text;

namespace PCL.Avalonia;

public static partial class DownloadUi
{
    /// <summary>
    ///     获取下载某个 Forgelike 实例的加载器列表（WPF DownloadUi/ForgelikeLoader.cs 移植）。
    /// </summary>
    private static List<Loading.LoaderBase> McDownloadForgelikeLoader(
        GameDownload.DlForgelikeEntry.ForgelikeType forgeType,
        string loaderVersion,
        string targetVersion,
        string inherit,
        GameDownload.DlForgelikeEntry? info = null,
        string? mcFolder = null,
        Loading.LoaderCombo<string>? clientDownloadLoader = null,
        string? clientFolder = null)
    {
        mcFolder = mcFolder ?? GameFolders.mcFolderSelected ?? "";
        info = _ResolveForgelikeInfo(forgeType, ref loaderVersion, ref inherit, info);

        var loaderName = LauncherCommon.GetStringFromEnum(forgeType) ?? forgeType.ToString();
        var isCustomFolder = (mcFolder ?? "") != (GameFolders.mcFolderSelected ?? "");
        var installerAddress = Path.Combine(
            LauncherCommon.GetTempFolder("Download"),
            "forge_installer.jar");
        var versionFolder = Path.Combine(mcFolder ?? "", "versions", targetVersion) + DirSep;
        var loaders = new List<Loading.LoaderBase>();

        if (info is null)
            loaders.Add(
                new Loading.LoaderTask<string, string>(
                    Lang.Text("Minecraft.Download.Stage.ObtainLoaderDetails", loaderName),
                    task => info = _FindForgelikeEntry(task, inherit, loaderVersion, loaderName)
                )
                {
                    ProgressWeight = 3d
                }
            );

        loaders.Add(
            new Loading.LoaderTask<string, List<DownloadFile>>(
                Lang.Text("Minecraft.Download.Stage.PrepareLoaderDownload", loaderName),
                task =>
                {
                    clientDownloadLoader = _EnsureForgelikeClientLoader(
                        clientDownloadLoader,
                        isCustomFolder,
                        inherit);
                    task.output = _CreateForgelikeDownloadFiles(
                        info ?? throw new InvalidOperationException("未获取到加载器信息。"),
                        installerAddress,
                        true);
                }
            )
            {
                ProgressWeight = 0.5d,
                show = false
            }
        );
        loaders.Add(
            new LoaderDownload(
                Lang.Text("Minecraft.Download.Stage.DownloadLoaderMainFile", loaderName),
                [])
            {
                ProgressWeight = 9d
            }
        );

        if (forgeType == GameDownload.DlForgelikeEntry.ForgelikeType.NeoForge
            || Convert.ToDouble(loaderVersion.BeforeFirst(".")) >= 20d)
            _AddModernForgelikeInstallLoaders(
                loaders,
                forgeType,
                loaderName,
                inherit,
                loaderVersion,
                targetVersion,
                installerAddress,
                versionFolder,
                mcFolder ?? "",
                isCustomFolder,
                clientFolder,
                () => clientDownloadLoader);
        else
            _AddLegacyForgelikeInstallLoader(
                loaders,
                forgeType,
                loaderVersion,
                installerAddress,
                versionFolder,
                targetVersion,
                mcFolder ?? "",
                inherit);

        return loaders;
    }

    private static GameDownload.DlForgelikeEntry? _ResolveForgelikeInfo(
        GameDownload.DlForgelikeEntry.ForgelikeType forgeType,
        ref string loaderVersion,
        ref string inherit,
        GameDownload.DlForgelikeEntry? info)
    {
        switch (forgeType)
        {
            case GameDownload.DlForgelikeEntry.ForgelikeType.NeoForge when info is null:
            {
                if (inherit == "1.20.1" && !loaderVersion.StartsWithF("1.20.1-"))
                    info = new GameDownload.DlNeoForgeListEntry($"1.20.1-{loaderVersion}");
                else
                    info = new GameDownload.DlNeoForgeListEntry(loaderVersion);
                break;
            }

            // 已有入口信息时直接使用（WPF 原版缺少此分支，info 非空时会误抛 ArgumentOutOfRange）
            case GameDownload.DlForgelikeEntry.ForgelikeType.NeoForge:
                break;

            case GameDownload.DlForgelikeEntry.ForgelikeType.Cleanroom when info is null:
                info = new GameDownload.DlCleanroomListEntry(loaderVersion);
                break;

            case GameDownload.DlForgelikeEntry.ForgelikeType.Cleanroom:
                break;

            case GameDownload.DlForgelikeEntry.ForgelikeType.Forge:
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(forgeType), forgeType, null);
        }

        if (forgeType == GameDownload.DlForgelikeEntry.ForgelikeType.NeoForge
            || !loaderVersion.StartsWithF("1.")
            || !loaderVersion.Contains('-'))
            return info;
        inherit = loaderVersion.BeforeFirst("-");
        loaderVersion = loaderVersion.AfterLast("-");

        return info;
    }

    private static GameDownload.DlForgelikeEntry _FindForgelikeEntry(
        Loading.LoaderTask<string, string> task,
        string inherit,
        string loaderVersion,
        string loaderName)
    {
        var forgeLoader = new Loading.LoaderTask<string, List<GameDownload.DlForgeVersionEntry>>(
            $"McDownloadForgeLoader {inherit}",
            GameDownload.DlForgeVersionMain
        );
        forgeLoader.WaitForExit(inherit);
        task.Progress = 0.8d;
        foreach (var forgeVersion in (forgeLoader.output ?? []).Where(forgeVersion =>
                     McVersionComparer.CompareVersion(
                         forgeVersion.version.ToString(),
                         loaderVersion)
                     == 0))
            return forgeVersion;

        throw new Exception(
            Lang.Text(
                "Minecraft.Download.Error.LoaderDetailsNotFound",
                loaderName,
                inherit,
                loaderVersion));
    }

    private static Loading.LoaderCombo<string>? _EnsureForgelikeClientLoader(
        Loading.LoaderCombo<string>? clientDownloadLoader,
        bool isCustomFolder,
        string inherit)
    {
        if (clientDownloadLoader is not null)
            return clientDownloadLoader;

        // WPF 版此处会走 McDownloadClient 预下载；Avalonia MVP 的合并安装流程总是传入 clientLoader，
        // 独立安装到自定义文件夹的场景不在 MVP 范围内。
        throw new Exception(
            isCustomFolder
                ? Lang.Text("Minecraft.Download.Error.NoVanillaLoaderSpecified")
                : "合并安装缺少原版下载加载器");
    }

    private static List<DownloadFile> _CreateForgelikeDownloadFiles(
        GameDownload.DlForgelikeEntry info,
        string target,
        bool normalizeForgeInherit
    )
    {
        var files = new List<DownloadFile>();
        switch (info.forgeType)
        {
            case GameDownload.DlForgelikeEntry.ForgelikeType.NeoForge:
            {
                var neo = (GameDownload.DlNeoForgeListEntry)info;
                var url = $"{neo.UrlBase}-installer.jar";
                files.Add(
                    new DownloadFile(
                        [
                            url.Replace(
                                "maven.neoforged.net/releases",
                                "bmclapi2.bangbang93.com/maven"),
                            url
                        ],
                        target,
                        new FileChecker(64 * 1024)
                    )
                );
                break;
            }

            case GameDownload.DlForgelikeEntry.ForgelikeType.Cleanroom:
            {
                var cleanroom = (GameDownload.DlCleanroomListEntry)info;
                var url = $"{cleanroom.UrlBase}-installer.jar";
                files.Add(new DownloadFile([url], target, new FileChecker(64 * 1024)));
                break;
            }

            case GameDownload.DlForgelikeEntry.ForgelikeType.Forge:
            default:
            {
                var forge = (GameDownload.DlForgeVersionEntry)info;
                var inheritName = normalizeForgeInherit
                    ? forge.Inherit.Replace("-", "_")
                    : forge.Inherit;
                var fileName =
                    $"{inheritName}-{forge.FileVersion}/forge-{inheritName}-{forge.FileVersion}-{forge.Category}.{forge.FileExtension}";
                files.Add(
                    new DownloadFile(
                        [
                            $"https://bmclapi2.bangbang93.com/maven/net/minecraftforge/forge/{fileName}",
                            $"https://files.minecraftforge.net/maven/net/minecraftforge/forge/{fileName}"
                        ],
                        target,
                        new FileChecker(64 * 1024, hash: forge.Hash)
                    )
                );
                break;
            }
        }

        return files;
    }

    private static void _AddModernForgelikeInstallLoaders(
        List<Loading.LoaderBase> loaders,
        GameDownload.DlForgelikeEntry.ForgelikeType forgeType,
        string loaderName,
        string inherit,
        string loaderVersion,
        string targetVersion,
        string installerAddress,
        string versionFolder,
        string mcFolder,
        bool isCustomFolder,
        string? clientFolder,
        Func<Loading.LoaderCombo<string>?> clientLoaderAccessor
    )
    {
        LauncherCommon.Log(
            $"[Download] 检测为{(forgeType == GameDownload.DlForgelikeEntry.ForgelikeType.Forge
                ? "新版 Forge"
                : $" {forgeType}")}：{loaderVersion}"
        );
        List<Libraries.McLibToken>? libs = null;
        loaders.Add(
            new Loading.LoaderTask<string, List<DownloadFile>>(
                Lang.Text("Minecraft.Download.Stage.AnalyzeLoaderLibraries", loaderName),
                task => libs = _AnalyzeForgelikeLibraries(
                    task,
                    installerAddress,
                    inherit,
                    loaderName,
                    loaderVersion,
                    forgeType)
            )
            {
                ProgressWeight = 2d
            }
        );
        loaders.Add(
            new LoaderDownload(
                Lang.Text("Minecraft.Download.Stage.DownloadLoaderLibraries", loaderName),
                [])
            {
                ProgressWeight = 12d
            }
        );
        loaders.Add(
            new Loading.LoaderTask<List<DownloadFile>, bool>(
                Lang.Text("Minecraft.Download.Stage.GetLoaderLibraries", loaderName),
                task =>
                    _ApplyForgelikeLibraries(
                        task,
                        libs ?? [],
                        isCustomFolder,
                        loaderName,
                        mcFolder,
                        clientFolder,
                        clientLoaderAccessor(),
                        inherit)
            )
            {
                ProgressWeight = 0.1d,
                show = false
            }
        );
        loaders.Add(
            new Loading.LoaderTask<bool, bool>(
                forgeType == GameDownload.DlForgelikeEntry.ForgelikeType.Forge
                    ? Lang.Text("Minecraft.Download.Stage.InstallForge.MethodA")
                    : Lang.Text("Minecraft.Download.Stage.InstallForgeType", forgeType),
                task => _InstallModernForgelike(
                    task,
                    installerAddress,
                    mcFolder,
                    targetVersion,
                    versionFolder,
                    loaderName,
                    forgeType)
            )
            {
                ProgressWeight = 10d
            }
        );
    }

    private static List<Libraries.McLibToken> _AnalyzeForgelikeLibraries(
        Loading.LoaderTask<string, List<DownloadFile>> task,
        string installerAddress,
        string inherit,
        string loaderName,
        string loaderVersion,
        GameDownload.DlForgelikeEntry.ForgelikeType forgeType
    )
    {
        task.output = [];
        ZipArchive? installer = null;
        try
        {
            WaitForFileReady(installerAddress);
            installer = new ZipArchive(new FileStream(installerAddress, FileMode.Open));
            task.Progress = 0.2d;
            var installProfileEntry = installer.GetEntry("install_profile.json")
                ?? throw new InvalidOperationException("安装器缺少 install_profile.json");
            var versionEntry = installer.GetEntry("version.json")
                ?? throw new InvalidOperationException("安装器缺少 version.json");
            var json = (JsonObject)LauncherCommon.GetJson(
                LauncherCommon.ReadFile(installProfileEntry.Open()));
            var json2 = (JsonObject)LauncherCommon.GetJson(
                LauncherCommon.ReadFile(versionEntry.Open()));
            json.Merge(json2);
            if (inherit == "1.16.5" && Config.Download.FixAuthLib)
                json = (JsonObject)
                    LauncherCommon.GetJson(
                        json
                            .ToString()
                            .Replace("2.1.28/authlib-2.1.28.jar", "2.3.31/authlib-2.3.31.jar")
                            .Replace("com.mojang:authlib:2.1.28", "com.mojang:authlib:2.3.31")
                            .Replace(
                                "ad54da276bf59983d02d5ed16fc14541354c71fd",
                                "bbd00ca33b052f73a6312254780fc580d2da3535")
                            .Replace("76328", "87662")
                    );
            var libs = Libraries.McLibListGetWithJson(json, true);
            if (json["data"]?["MOJMAPS"] != null)
            {
                task.Progress = 0.4d;
                var clientListUrl = GameDownload.DlClientListGet(inherit)?.ToString()
                    ?? throw new InvalidOperationException("未获取到 Minecraft Json 地址");
                var rawJson = (JsonObject)
                    LauncherCommon.GetJson(
                        NetFacade.NetGetCodeByLoader(
                            GameDownload.DlSourceLauncherOrMetaGet(clientListUrl),
                            isJson: true
                        )
                    );
                var mappingsClientNode = json["data"]?["MOJMAPS"]?["client"]
                    ?? throw new InvalidOperationException("未找到 MOJMAPS 客户端映射");
                var mappingsClient = mappingsClientNode
                    .ToString()
                    .Trim("[]".ToCharArray());
                var originalName = mappingsClient.BeforeFirst("@");
                var address = Libraries
                    .McLibGet(originalName)
                    .Replace(
                        ".jar",
                        $"-mappings.{mappingsClient.Split("@")[1]}");
                var clientMappings = rawJson["downloads"]?["client_mappings"]
                    ?? throw new InvalidOperationException("未找到客户端映射下载信息");
                libs.Add(
                    new Libraries.McLibToken
                    {
                        IsNatives = false,
                        LocalPath = address,
                        OriginalName = originalName,
                        Url = clientMappings["url"]?.ToString() ?? "",
                        size = (long?)clientMappings["size"] ?? -1L,
                        Sha1 = clientMappings["sha1"]?.ToString() ?? ""
                    }
                );
                LauncherCommon.Log(
                    $"[Download] 需要下载 Mappings：{clientMappings["url"]} (SHA1: {clientMappings["sha1"]})");
            }

            task.Progress = 0.8d;
            for (var i = 0; i < libs.Count; i++)
                if (libs[i]
                        .LocalPath.EndsWithF(
                            $"{loaderName.ToLower()}-{inherit}-{loaderVersion}.jar")
                    || libs[i]
                        .LocalPath.EndsWithF(
                            $"{loaderName.ToLower()}-{inherit}-{loaderVersion}-client.jar"))
                {
                    LauncherCommon.Log(
                        $"[Download] 已从待下载 {loaderName} 支持库中移除：{libs[i].LocalPath}",
                        LauncherLogLevel.Debug);
                    libs.RemoveAt(i);
                    break;
                }

            task.output = Libraries.McLibNetFilesFromTokens(libs);
            return libs;
        }
        catch (Exception ex)
        {
            throw new Exception(
                Lang.Text(
                    "Minecraft.Download.Error.LoaderLibraryListFailed",
                    forgeType == GameDownload.DlForgelikeEntry.ForgelikeType.Forge
                        ? Lang.Text("Minecraft.Download.Loader.NewForge")
                        : $" {forgeType}"
                ),
                ex
            );
        }
        finally
        {
            installer?.Dispose();
        }
    }

    private static void _ApplyForgelikeLibraries(
        Loading.LoaderTask<List<DownloadFile>, bool> task,
        List<Libraries.McLibToken> libs,
        bool isCustomFolder,
        string loaderName,
        string mcFolder,
        string? clientFolder,
        Loading.LoaderCombo<string>? clientDownloadLoader,
        string inherit
    )
    {
        if (isCustomFolder)
        {
            if (GameFolders.mcFolderSelected is not { } selectedMcFolder)
                return;
            foreach (var libFile in libs)
            {
                var realPath = libFile.LocalPath.Replace(selectedMcFolder, mcFolder);
                if (!File.Exists(realPath))
                {
                    var realPathFolder = Path.GetDirectoryName(realPath);
                    if (realPathFolder is not null)
                        Directory.CreateDirectory(realPathFolder);
                    LauncherCommon.CopyFile(libFile.LocalPath, realPath);
                }

                if (LauncherCommon.ModeDebug)
                    LauncherCommon.Log($"[Download] 复制的 {loaderName} 支持库文件：{libFile.LocalPath}");
            }
        }

        if (clientDownloadLoader is null)
            return;

        var targetLoaders = clientDownloadLoader
            .GetLoaderList()
            .Where(l => l.Identifier is McDownloadClientLibId or McDownloadClientJsonId)
            .Where(l => l.State != LoadState.Finished)
            .ToList();
        if (targetLoaders.Count != 0)
            LauncherCommon.Log($"[Download] {loaderName} 安装正在等待原版文件下载完成");
        while (targetLoaders.Count != 0 && !task.IsAborted)
        {
            targetLoaders = [.. targetLoaders.Where(l => l.State != LoadState.Finished)];
            Thread.Sleep(50);
        }

        if (task.IsAborted)
            return;
        if (!isCustomFolder || clientFolder is null)
            return;

        lock (VanillaSyncLock)
        {
            var clientName = LauncherCommon.GetFolderNameFromPath(clientFolder);
            var inheritedVersionFolder = Path.Combine(mcFolder, "versions", inherit);
            Directory.CreateDirectory(inheritedVersionFolder);
            if (!File.Exists(Path.Combine(inheritedVersionFolder, $"{inherit}.json")))
                LauncherCommon.CopyFile(
                    Path.Combine(clientFolder, $"{clientName}.json"),
                    Path.Combine(inheritedVersionFolder, $"{inherit}.json"));
            if (!File.Exists(Path.Combine(inheritedVersionFolder, $"{inherit}.jar")))
                LauncherCommon.CopyFile(
                    Path.Combine(clientFolder, $"{clientName}.jar"),
                    Path.Combine(inheritedVersionFolder, $"{inherit}.jar"));
        }
    }

    private static void _InstallModernForgelike(
        Loading.LoaderTask<bool, bool> task,
        string installerAddress,
        string mcFolder,
        string targetVersion,
        string versionFolder,
        string loaderName,
        GameDownload.DlForgelikeEntry.ForgelikeType forgeType
    )
    {
        WaitForFileReady(installerAddress);
        var installer = new ZipArchive(new FileStream(installerAddress, FileMode.Open));
        try
        {
            LauncherCommon.Log($"[Download] 开始进行 Forgelike 安装：{installerAddress}");
            var oldList = new DirectoryInfo(Path.Combine(mcFolder, "versions"))
                .EnumerateDirectories()
                .Select(i => i.FullName)
                .ToList();

            var installProfileEntry = installer.GetEntry("install_profile.json")
                ?? throw new InvalidOperationException("安装器缺少 install_profile.json");
            _ = LauncherCommon.GetJson(LauncherCommon.ReadFile(installProfileEntry.Open()));
            Directory.CreateDirectory(versionFolder);
            task.Progress = 0.04d;
            GameFolders.McFolderLauncherProfilesJsonCreate(mcFolder);
            task.Progress = 0.05d;
            var useJavaWrapper = LauncherCommon.IsUtf8CodePage();

            _RunForgelikeInstallerWithRetry(
                task,
                installerAddress,
                mcFolder,
                useJavaWrapper,
                forgeType,
                loaderName);

            task.Progress = 0.97d;
            _CopyGeneratedForgelikeInstance(mcFolder, oldList, versionFolder, targetVersion);
        }
        catch (Exception ex)
        {
            throw new Exception(
                Lang.Text("Minecraft.Download.Error.LoaderInstallFailed", loaderName),
                ex);
        }
        finally
        {
            try
            {
                installer.Dispose();
                if (File.Exists(installerAddress))
                    File.Delete(installerAddress);
            }
            catch (Exception ex)
            {
                LauncherCommon.Log(ex, $"安装 {loaderName} 清理文件时出错");
            }
        }
    }

    private static void _RunForgelikeInstallerWithRetry(
        Loading.LoaderTask<bool, bool> task,
        string installerAddress,
        string mcFolder,
        bool useJavaWrapper,
        GameDownload.DlForgelikeEntry.ForgelikeType forgeType,
        string loaderName
    )
    {
        var useWrapper = useJavaWrapper;
        while (true)
            try
            {
                LauncherCommon.WriteFile(
                    Path.Combine(LauncherPaths.Temp, "Cache", "forge_installer.jar"),
                    LauncherCommon.GetResourceStream("Resources/forge-installer.jar")
                    ?? throw new InvalidOperationException("缺少 forge-installer.jar 资源")
                );
                task.Progress = 0.06d;
                ForgelikeInjector(installerAddress, task, mcFolder, useWrapper, forgeType);
                return;
            }
            catch (Exception ex)
            {
                if (useWrapper)
                    throw new Exception(
                        Lang.Text("Minecraft.Download.Error.LoaderInstallerRunFailed", loaderName),
                        ex);

                LauncherCommon.Log(ex, $"不使用 JavaWrapper 安装 {loaderName} 失败，将使用 JavaWrapper 并重试");
                useWrapper = true;
            }
    }

    private static void _CopyGeneratedForgelikeInstance(
        string mcFolder,
        List<string> oldList,
        string versionFolder,
        string targetVersion)
    {
        var deltaList = new DirectoryInfo(Path.Combine(mcFolder, "versions"))
            .EnumerateDirectories()
            .SkipWhile(i => oldList.Contains(i.FullName))
            .ToList();

        if (deltaList.Count > 1)
            deltaList =
            [
                .. deltaList.Where(l =>
                    l.Name.ContainsF("forge", true) && l.EnumerateFiles().Any())
            ];
        switch (deltaList.Count)
        {
            case 1:
            {
                var jsonFile = deltaList[0].EnumerateFiles().First();
                LauncherCommon.WriteFile(
                    Path.Combine(versionFolder, $"{targetVersion}.json"),
                    LauncherCommon.ReadFile(jsonFile.FullName));
                LauncherCommon.Log(
                    $"[Download] 已拷贝新增的实例 Json 文件：{jsonFile.FullName} -> {versionFolder}{targetVersion}.json");
                break;
            }
            case > 1:
                LauncherCommon.Log(
                    $"[Download] 有多个疑似的新增实例，无法确定：{string.Join(
                        ";",
                        deltaList.Select<DirectoryInfo, string>(d => d.Name))}"
                );
                break;
            default:
                LauncherCommon.Log("[Download] 未找到新增的实例文件夹");
                break;
        }
    }

    #region 旧版 Forge（< 20）安装：纯解压，无需 Java

    private static void _AddLegacyForgelikeInstallLoader(
        List<Loading.LoaderBase> loaders,
        GameDownload.DlForgelikeEntry.ForgelikeType forgeType,
        string loaderVersion,
        string installerAddress,
        string versionFolder,
        string targetVersion,
        string mcFolder,
        string inherit
    )
    {
        LauncherCommon.Log($"[Download] 检测为非新版 Forge：{loaderVersion}");
        loaders.Add(
            new Loading.LoaderTask<List<DownloadFile>, bool>(
                forgeType == GameDownload.DlForgelikeEntry.ForgelikeType.Forge
                    ? Lang.Text("Minecraft.Download.Stage.InstallForge.MethodB")
                    : Lang.Text("Minecraft.Download.Stage.InstallForgeType", forgeType),
                task => _InstallLegacyForgelike(
                    task,
                    installerAddress,
                    versionFolder,
                    targetVersion,
                    mcFolder,
                    inherit)
            )
            {
                ProgressWeight = 1d
            }
        );
    }

    private static void _InstallLegacyForgelike(
        Loading.LoaderTask<List<DownloadFile>, bool> task,
        string installerAddress,
        string versionFolder,
        string targetVersion,
        string mcFolder,
        string inherit
    )
    {
        ZipArchive? installer = null;
        try
        {
            WaitForFileReady(installerAddress);
            installer = new ZipArchive(new FileStream(installerAddress, FileMode.Open));
            task.Progress = 0.2d;
            var installProfileEntry = installer.GetEntry("install_profile.json")
                ?? throw new InvalidOperationException("安装器缺少 install_profile.json");
            var json = (JsonObject)LauncherCommon.GetJson(
                LauncherCommon.ReadFile(installProfileEntry.Open()));
            task.Progress = 0.4d;
            Directory.CreateDirectory(versionFolder);
            task.Progress = 0.5d;
            if (json["install"] is null)
                _InstallLegacyForgelikeMethodOne(
                    task,
                    installer,
                    installerAddress,
                    versionFolder,
                    targetVersion,
                    mcFolder,
                    json);
            else
                _InstallLegacyForgelikeMethodTwo(
                    task,
                    installer,
                    installerAddress,
                    versionFolder,
                    targetVersion,
                    mcFolder,
                    inherit,
                    json);
        }
        catch (Exception ex)
        {
            throw new Exception(Lang.Text("Minecraft.Download.Error.ForgeOldInstallFailed"), ex);
        }
        finally
        {
            try
            {
                installer?.Dispose();
                if (File.Exists(installerAddress))
                    File.Delete(installerAddress);
                var unrarDir = Path.Combine(
                    Path.GetDirectoryName(installerAddress)
                    ?? throw new InvalidOperationException("安装器路径无效"),
                    "_unrar");
                if (Directory.Exists(unrarDir))
                    LauncherCommon.DeleteDirectory(unrarDir);
            }
            catch (Exception ex)
            {
                LauncherCommon.Log(ex, "非新版方式安装 Forge 清理文件时出错");
            }
        }
    }

    private static void _InstallLegacyForgelikeMethodOne(
        Loading.LoaderTask<List<DownloadFile>, bool> task,
        ZipArchive installer,
        string installerAddress,
        string versionFolder,
        string targetVersion,
        string mcFolder,
        JsonObject json
    )
    {
        LauncherCommon.Log($"[Download] 开始进行 Forge 安装，Legacy 方式 1：{installerAddress}");
        var versionEntryName = json["json"]?.ToString()
            ?? throw new InvalidOperationException("安装器缺少版本 Json 信息");
        var versionEntry = installer.GetEntry(versionEntryName.TrimStart('/'))
            ?? throw new InvalidOperationException("安装器缺少版本 Json 文件");
        var jsonVersion = (JsonObject)LauncherCommon.GetJson(
            LauncherCommon.ReadFile(versionEntry.Open()));
        jsonVersion["id"] = targetVersion;
        LauncherCommon.WriteFile(
            Path.Combine(versionFolder, $"{targetVersion}.json"),
            jsonVersion.ToString());
        task.Progress = 0.6d;
        installer.Dispose();
        var unrarDir = Path.Combine(
            Path.GetDirectoryName(installerAddress)
            ?? throw new InvalidOperationException("安装器路径无效"),
            "_unrar");
        LauncherCommon.ExtractFile(installerAddress, unrarDir);
        LauncherCommon.CopyDirectory(Path.Combine(unrarDir, "maven"), Path.Combine(mcFolder, "libraries"));
        LauncherCommon.DeleteDirectory(unrarDir);
    }

    private static void _InstallLegacyForgelikeMethodTwo(
        Loading.LoaderTask<List<DownloadFile>, bool> task,
        ZipArchive installer,
        string installerAddress,
        string versionFolder,
        string targetVersion,
        string mcFolder,
        string inherit,
        JsonObject json
    )
    {
        LauncherCommon.Log($"[Download] 开始进行 Forge 安装，Legacy 方式 2：{installerAddress}");
        var installInfo = json["install"]
            ?? throw new InvalidOperationException("安装器缺少 install 信息");
        var jarAddress = Libraries.McLibGet(
            installInfo["path"]?.ToString()
            ?? throw new InvalidOperationException("安装器缺少安装路径"),
            customMcFolder: mcFolder);
        if (File.Exists(jarAddress))
            File.Delete(jarAddress);
        var installEntryName = installInfo["filePath"]?.ToString()
            ?? throw new InvalidOperationException("安装器缺少安装文件名");
        LauncherCommon.WriteFile(
            jarAddress,
            (installer.GetEntry(installEntryName)
                ?? throw new InvalidOperationException("安装器缺少安装文件")).Open());
        task.Progress = 0.9d;
        var versionInfo = json["versionInfo"]
            ?? throw new InvalidOperationException("安装器缺少 versionInfo 信息");
        versionInfo["id"] = targetVersion;
        if (versionInfo["inheritsFrom"] is null)
            ((JsonObject)versionInfo).Add("inheritsFrom", inherit);
        LauncherCommon.WriteFile(
            Path.Combine(versionFolder, $"{targetVersion}.json"),
            versionInfo.ToString());
    }

    #endregion

    #region Java 安装器进程（ForgelikeInjector）

    private static void ForgelikeInjector(
        string target,
        Loading.LoaderTask<bool, bool> task,
        string mcFolder,
        bool useJavaWrapper,
        GameDownload.DlForgelikeEntry.ForgelikeType forgeType
    )
    {
        var java = _SelectForgelikeJava(task);
        if (java is null)
            return;

        var arguments = _BuildForgelikeInstallArguments(target, mcFolder, useJavaWrapper, java);
        lock (InstallSyncLock)
        {
            var info = new ProcessStartInfo
            {
                FileName = java.Installation.JavaExePath,
                Arguments = arguments,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true,
                RedirectStandardOutput = true
            };
            var loaderName = LauncherCommon.GetStringFromEnum(forgeType) ?? forgeType.ToString();
            LauncherCommon.Log($"[Download] 开始安装 {loaderName}：{arguments}");
            var process = new Process { StartInfo = info };
            var lastResults = new Queue<string>();
            using var outputWaitHandle = new AutoResetEvent(false);
            using var errorWaitHandle = new AutoResetEvent(false);
            process.OutputDataReceived += _CreateForgelikeOutputHandler(
                process,
                task,
                outputWaitHandle,
                loaderName,
                lastResults,
                $"读取 {loaderName} 安装器信息失败"
            );
            process.ErrorDataReceived += _CreateForgelikeOutputHandler(
                process,
                task,
                errorWaitHandle,
                loaderName,
                lastResults,
                $"读取 {loaderName} 安装器错误信息失败"
            );
            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            while (!process.HasExited)
                Thread.Sleep(10);
            outputWaitHandle.WaitOne(10000);
            errorWaitHandle.WaitOne(10000);
            process.Dispose();
            if (lastResults.Reverse().Take(5).Any(l => l == "true"))
                return;
            LauncherCommon.Log(lastResults.Join("\r\n"));
            throw new Exception(
                Lang.Text(
                    "Minecraft.Download.Error.LoaderInstallerFailedLastLine",
                    loaderName,
                    _GetForgelikeInstallerLastLines(lastResults)
                )
            );
        }
    }

    private static JavaEntry? _SelectForgelikeJava(Loading.LoaderTask<bool, bool> task)
    {
        JavaEntry java;
        lock (JavaRuntime.JavaLock)
        {
            java = JavaRuntime.JavaSelect(
                Lang.Text("Minecraft.Download.Error.InstallationCanceled"),
                new Version(1, 8, 0, 60));
            if (java is not null)
                return java;

            if (!JavaRuntime.JavaDownloadConfirm(
                    Lang.Text("Minecraft.Download.Error.JavaVersionRequired")))
                throw new Exception(
                    Lang.Text("Minecraft.Download.Error.JavaNotFoundInstallCanceled"));

            var javaLoader = JavaRuntime.GetJavaDownloadLoader();
            try
            {
                javaLoader.Start(17, true);
                while (javaLoader.State == LoadState.Loading && !task.IsAborted)
                    Thread.Sleep(10);
            }
            finally
            {
                javaLoader.Abort();
            }

            java = JavaRuntime.JavaSelect(
                Lang.Text("Minecraft.Download.Error.InstallationCanceled"),
                new Version(1, 8, 0, 60));
            if (task.IsAborted)
                return null;
            if (java is null)
                throw new Exception(
                    Lang.Text("Minecraft.Download.Error.JavaNotFoundInstallCanceled"));
        }

        return java;
    }

    private static string _BuildForgelikeInstallArguments(
        string target,
        string mcFolder,
        bool useJavaWrapper,
        JavaEntry java)
    {
        // classpath 分隔符按平台（Windows ';' / Unix ':'）
        var cpSeparator = Path.PathSeparator;
        var forgeInstallerJar = Path.Combine(LauncherPaths.Temp, "Cache", "forge_installer.jar");
        string arguments;
        if (useJavaWrapper && !Config.Launch.DisableJlw)
            arguments =
                $"-Doolloo.jlw.tmpdir=\"{LauncherPaths.PureAsciiDirectory.TrimEnd(DirSep)}\" " +
                $"-cp \"{forgeInstallerJar}{cpSeparator}{target}\" " +
                $"-jar \"{GameLaunch.ExtractJavaWrapper()}\" com.bangbang93.ForgeInstaller \"{mcFolder}\"";
        else
            arguments =
                $"-cp \"{forgeInstallerJar}{cpSeparator}{target}\" com.bangbang93.ForgeInstaller \"{mcFolder}\"";

        if (java.Installation.MajorVersion >= 9)
            arguments =
                $"--add-exports cpw.mods.bootstraplauncher/cpw.mods.bootstraplauncher=ALL-UNNAMED {arguments}";

        return arguments;
    }

    private static DataReceivedEventHandler _CreateForgelikeOutputHandler(
        Process process,
        Loading.LoaderTask<bool, bool> task,
        AutoResetEvent waitHandle,
        string loaderName,
        Queue<string> lastResults,
        string readErrorMessage
    )
    {
        return (_, e) =>
        {
            try
            {
                if (e.Data is null)
                {
                    waitHandle.Set();
                }
                else
                {
                    lastResults.Enqueue(e.Data);
                    if (lastResults.Count > 100)
                        lastResults.Dequeue();
                    ForgelikeInjectorLine(e.Data, task);
                }
            }
            catch (ObjectDisposedException)
            {
            }
            catch (Exception ex)
            {
                LauncherCommon.Log(ex, readErrorMessage);
            }

            try
            {
                if (task.State == LoadState.Aborted && !process.HasExited)
                {
                    LauncherCommon.Log($"[Installer] 由于任务取消，已中止 {loaderName} 安装");
                    process.Kill();
                }
            }
            catch
            {
                // ignored
            }
        };
    }

    private static string _GetForgelikeInstallerLastLines(Queue<string> lastResults)
    {
        var lastLines = "";
        for (var i = Math.Max(0, lastResults.Count - 5); i <= lastResults.Count - 1; i++)
            lastLines += $"\r\n{lastResults.ElementAtOrDefault(i)}";
        return lastLines;
    }

    private static void ForgelikeInjectorLine(string content, Loading.LoaderTask<bool, bool> task)
    {
        switch (content ?? "")
        {
            case "Extracting json":
            {
                LauncherCommon.Log($"[Installer] {content}");
                task.Progress = 0.07d;
                break;
            }
            case "Downloading libraries":
            {
                LauncherCommon.Log($"[Installer] {content}");
                task.Progress = 0.08d;
                break;
            }
            case "  File exists: Checksum validated.":
            {
                if (LauncherCommon.ModeDebug)
                    LauncherCommon.Log($"[Installer] {content}");
                task.Progress += 0.003d;
                break;
            }
            case "Building Processors":
            {
                task.Progress = 0.18d;
                break;
            }
            case "Task: DOWNLOAD_MOJMAPS":
            {
                task.Progress = 0.2d;
                break;
            }
            case "Task: MERGE_MAPPING":
            {
                task.Progress = 0.3d;
                break;
            }
            case "Splitting: ":
            {
                task.Progress = 0.35d;
                break;
            }
            case "Parameter Annotations":
            {
                task.Progress = 0.4d;
                break;
            }
            case "Processing Complete":
            case "log: null":
            {
                task.Progress = 0.5d;
                break;
            }
            case "Sorting":
            {
                task.Progress = 0.65d;
                break;
            }
            case "Remapping final jar":
            {
                task.Progress = 0.72d;
                break;
            }
            case "Remapping jar... 50%":
            {
                task.Progress = 0.76d;
                break;
            }
            case "Remapping jar... 100%":
            {
                task.Progress = 0.81d;
                break;
            }
            case "Injecting profile":
            {
                task.Progress = 0.91d;
                break;
            }

            default:
            {
                if (LauncherCommon.ModeDebug)
                    LauncherCommon.Log($"[Installer] {content}");
                return;
            }
        }

        LauncherCommon.Log($"[Installer] {content}");
    }

    #endregion
}
