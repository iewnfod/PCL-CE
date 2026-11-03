using System.IO;
using System.Text.Json.Nodes;
using PCL.Core.App;
using PCL.Core.App.Localization;
using PCL.Core.UI.Hints;
using PCL.Network;
using PCL.Network.Loaders;
using PCL.Text;

namespace PCL.Avalonia;

public static partial class DownloadUi
{
    /// <summary>
    ///     获取合并安装加载器列表，并进行前期的缓存清理工作。
    /// </summary>
    /// <exception cref="CancelledException" />
    public static List<Loading.LoaderBase> McInstallLoader(
        McInstallRequest request,
        bool dontFixLibraries = false,
        bool ignoreDump = false)
    {
        // 安装 Mod 加载器的临时文件夹（Linux 临时目录不含空格，无需 requireNonSpace 区分）
        var tempMcFolder = LauncherCommon.GetTempFolder("Download");

        var minecraftName = request.minecraftName
            ?? throw new Exception(Lang.Text("Minecraft.Download.Modpack.MissingGameVersion.Generic"));
        var instanceFolder = GetInstanceFolderPath(request.targetInstanceName);
        if (Directory.Exists(tempMcFolder))
            LauncherCommon.DeleteDirectory(tempMcFolder);

        var folders = _PrepareMcInstallFolders(request, tempMcFolder, minecraftName);
        _LogMcInstallFolders(folders, minecraftName);
        LauncherCommon.Log("[Diag] folders 就绪，检查实例是否已存在", LauncherLogLevel.Debug);

        if (File.Exists(Path.Combine(instanceFolder, $"{request.targetInstanceName}.json"))
            && !ignoreDump)
        {
            HintService.Show(Hints.Error(Lang.Text(
                "Minecraft.Download.Error.InstanceAlreadyExists",
                request.targetInstanceName,
                "")));
            throw new CancelledException();
        }

        var loaderList = new List<Loading.LoaderBase>
        {
            _CreateIgnoreFlagLoader(instanceFolder)
        };
        _AddApiDownloadLoaders(loaderList, request, folders.ModsTempFolder);
        LauncherCommon.Log(
            $"[Diag] API 下载 loader 已加入（fabricApi={request.fabricApi?.DisplayName ?? "null"}，count={loaderList.Count}）",
            LauncherLogLevel.Debug);

        var clientLoader = _AddClientLoader(loaderList, request, minecraftName);
        if (clientLoader is not null)
            _AddLoaderSpecificLoaders(loaderList, request, tempMcFolder, folders, clientLoader, minecraftName);
        LauncherCommon.Log("[Diag] 加载器专属 loader 已加入", LauncherLogLevel.Debug);

        loaderList.Add(_CreateMergeInstallLoader(instanceFolder, tempMcFolder, folders, request));
        if (!dontFixLibraries && _ShouldFixInstallLibraries(request))
            _AddLibraryFixLoader(loaderList, request, instanceFolder, minecraftName);
        loaderList.Add(_CreateDeleteIgnoreFlagLoader(instanceFolder));
        LauncherCommon.Log($"[Diag] 全部 loader 就绪（{loaderList.Count}），返回", LauncherLogLevel.Debug);
        return loaderList;
    }

    private static McInstallFolders _PrepareMcInstallFolders(
        McInstallRequest request,
        string tempMcFolder,
        string minecraftName)
    {
        var folders = new McInstallFolders();

        if (request.forgeEntry is not null)
            request.forgeVersion = request.forgeVersion ?? request.forgeEntry.VersionName;
        if (request.forgeVersion is not null)
            folders.ForgeFolder = Path.Combine(tempMcFolder, "versions", $"forge-{request.forgeVersion}");

        if (request.neoForgeEntry is not null)
            request.neoForgeVersion = request.neoForgeVersion ?? request.neoForgeEntry.VersionName;
        if (request.neoForgeVersion is not null)
            folders.NeoForgeFolder =
                Path.Combine(tempMcFolder, "versions", $"neoforge-{request.neoForgeVersion}");

        if (request.cleanroomEntry is not null)
            request.cleanroomVersion = request.cleanroomVersion ?? request.cleanroomEntry.VersionName;
        if (request.cleanroomVersion is not null)
            folders.CleanroomFolder =
                Path.Combine(tempMcFolder, "versions", $"cleanroom-{request.cleanroomVersion}");

        if (request.fabricVersion is not null)
            folders.FabricFolder = Path.Combine(
                tempMcFolder,
                "versions",
                $"fabric-loader-{request.fabricVersion}-{minecraftName}");

        folders.Modable =
            request.fabricVersion is not null
            || request.forgeEntry is not null
            || request.neoForgeEntry is not null;
        folders.ModsTempFolder = Path.Combine(tempMcFolder, "mods") + DirSep;

        return folders;
    }

    private static void _LogMcInstallFolders(McInstallFolders folders, string minecraftName)
    {
        if (folders.ForgeFolder is not null)
            LauncherCommon.Log($"[Download] Forge 缓存：{folders.ForgeFolder}");
        if (folders.NeoForgeFolder is not null)
            LauncherCommon.Log($"[Download] NeoForge 缓存：{folders.NeoForgeFolder}");
        if (folders.CleanroomFolder is not null)
            LauncherCommon.Log($"[Download] Cleanroom 缓存：{folders.CleanroomFolder}");
        if (folders.FabricFolder is not null)
            LauncherCommon.Log($"[Download] Fabric 缓存：{folders.FabricFolder}");
        LauncherCommon.Log($"[Download] 对应的原版版本：{minecraftName}");
    }

    private static Loading.LoaderTask<int, int> _CreateIgnoreFlagLoader(string instanceFolder)
    {
        return new Loading.LoaderTask<int, int>(
            Lang.Text("Minecraft.Download.Stage.AddIgnoreFlag"),
            _ => LauncherCommon.WriteFile(
                Path.Combine(instanceFolder, ".pclignore"),
                "用于临时地在 PCL 的实例列表中屏蔽此实例。")
        )
        {
            show = false,
            block = false
        };
    }

    private static Loading.LoaderTask<int, int> _CreateDeleteIgnoreFlagLoader(string instanceFolder)
    {
        return new Loading.LoaderTask<int, int>(
            Lang.Text("Minecraft.Download.Stage.DeleteIgnoreFlag"),
            _ => File.Delete(Path.Combine(instanceFolder, ".pclignore"))
        )
        {
            show = false
        };
    }

    private static void _AddApiDownloadLoaders(
        List<Loading.LoaderBase> loaderList,
        McInstallRequest request,
        string modsTempFolder)
    {
        if (request.fabricApi is not null)
            loaderList.Add(_CreateApiDownloadLoader(
                "Minecraft.Download.Stage.DownloadFabricApi",
                request.fabricApi,
                modsTempFolder));
    }

    private static LoaderDownload _CreateApiDownloadLoader(
        string stageName,
        Comp.CompFile api,
        string modsTempFolder)
    {
        return new LoaderDownload(
            Lang.Text(stageName),
            [
                api.ToNetFile(
                    modsTempFolder,
                    Comp.DownloadReason.Dependency,
                    null,
                    Comp.CompLoaderType.Fabric)
            ]
        )
        {
            ProgressWeight = 3d,
            block = false
        };
    }

    private static Loading.LoaderCombo<string> _AddClientLoader(
        List<Loading.LoaderBase> loaderList,
        McInstallRequest request,
        string minecraftName
    )
    {
        var clientLoader = new Loading.LoaderCombo<string>(
            Lang.Text(
                "Minecraft.Download.Stage.LoaderDownloadCombo",
                Lang.Text("Minecraft.Version.Vanilla"),
                minecraftName),
            McDownloadClientLoader(
                minecraftName,
                request.minecraftJson,
                request.targetInstanceName)
        )
        {
            show = false,
            ProgressWeight = 39d,
            block =
                request.forgeVersion is null
                && request.neoForgeVersion is null
                && request.fabricVersion is null
                && request.cleanroomEntry is null
        };
        loaderList.Add(clientLoader);
        return clientLoader;
    }

    private static void _AddLoaderSpecificLoaders(
        List<Loading.LoaderBase> loaderList,
        McInstallRequest request,
        string tempMcFolder,
        McInstallFolders folders,
        Loading.LoaderCombo<string> clientLoader,
        string minecraftName
    )
    {
        if (request.forgeVersion is not null)
            _AddForgelikeLoader(
                loaderList,
                GameDownload.DlForgelikeEntry.ForgelikeType.Forge,
                "Forge",
                request.forgeVersion,
                $"forge-{request.forgeVersion}",
                minecraftName,
                request.forgeEntry,
                tempMcFolder,
                clientLoader,
                request.targetInstanceFolder,
                request.fabricVersion is null
                && request.neoForgeEntry is null
                && request.cleanroomEntry is null
            );

        if (request.neoForgeVersion is not null)
            _AddForgelikeLoader(
                loaderList,
                GameDownload.DlForgelikeEntry.ForgelikeType.NeoForge,
                "NeoForge",
                request.neoForgeVersion,
                $"neoforge-{request.neoForgeVersion}",
                minecraftName,
                request.neoForgeEntry,
                tempMcFolder,
                clientLoader,
                request.targetInstanceFolder,
                request.forgeEntry is null
                && request.fabricVersion is null
                && request.cleanroomEntry is null
            );

        if (request.cleanroomVersion is not null)
            _AddForgelikeLoader(
                loaderList,
                GameDownload.DlForgelikeEntry.ForgelikeType.Cleanroom,
                "Cleanroom",
                request.cleanroomVersion,
                $"cleanroom-{request.cleanroomVersion}",
                minecraftName,
                request.cleanroomEntry,
                tempMcFolder,
                clientLoader,
                request.targetInstanceFolder,
                request.forgeEntry is null
                && request.fabricVersion is null
                && request.neoForgeEntry is null
            );

        if (request.fabricVersion is not null)
            _AddFabricLoader(loaderList, request, tempMcFolder, minecraftName);
    }

    private static void _AddForgelikeLoader(
        List<Loading.LoaderBase> loaderList,
        GameDownload.DlForgelikeEntry.ForgelikeType forgeType,
        string displayName,
        string loaderVersion,
        string targetVersion,
        string minecraftName,
        GameDownload.DlForgelikeEntry? entry,
        string tempMcFolder,
        Loading.LoaderCombo<string> clientLoader,
        string targetInstanceFolder,
        bool block
    )
    {
        loaderList.Add(
            new Loading.LoaderCombo<string>(
                Lang.Text(
                    "Minecraft.Download.Stage.LoaderDownloadCombo",
                    displayName,
                    loaderVersion),
                McDownloadForgelikeLoader(
                    forgeType,
                    loaderVersion,
                    targetVersion,
                    minecraftName,
                    entry,
                    tempMcFolder,
                    clientLoader,
                    targetInstanceFolder)
            )
            {
                show = false,
                ProgressWeight = 25d,
                block = block
            }
        );
    }

    private static void _AddFabricLoader(
        List<Loading.LoaderBase> loaderList,
        McInstallRequest request,
        string tempMcFolder,
        string minecraftName)
    {
        if (request.fabricVersion is not { } fabricVersion)
            return;
        loaderList.Add(
            new Loading.LoaderCombo<string>(
                Lang.Text(
                    "Minecraft.Download.Stage.LoaderDownloadCombo",
                    "Fabric",
                    fabricVersion),
                McDownloadFabricLoader(fabricVersion, minecraftName, tempMcFolder, false)
            )
            {
                show = false,
                ProgressWeight = 2d,
                block = true
            }
        );
    }

    /// <summary>
    ///     获取下载某个 Minecraft 版本的加载器列表（WPF DownloadUi/CoreLoader.cs 移植）。
    /// </summary>
    private static List<Loading.LoaderBase> McDownloadClientLoader(
        string id,
        string? jsonUrl = null,
        string? instanceName = null)
    {
        instanceName ??= id;
        var instanceFolder = GetInstanceFolderPath(instanceName);

        var loaders = new List<Loading.LoaderBase>();

        if (jsonUrl is null)
            loaders.Add(
                new Loading.LoaderTask<string, List<DownloadFile>>(
                    Lang.Text("Minecraft.Download.Stage.ObtainVanillaJsonUrl"),
                    task =>
                    {
                        var jsonAddress = GameDownload.DlClientListGet(id)?.ToString();
                        if (jsonAddress is null)
                            throw new Exception(Lang.Text("Minecraft.Download.Error.NoJsonDownloadAddress"));
                        task.output =
                        [
                            new DownloadFile(
                                GameDownload.DlSourceLauncherOrMetaGet(jsonAddress),
                                Path.Combine(instanceFolder, $"{instanceName}.json")
                            )
                        ];
                    }
                )
                {
                    ProgressWeight = 2d,
                    show = false
                }
            );
        loaders.Add(
            new LoaderDownload(
                Lang.Text("Minecraft.Download.Stage.VanillaJsonDownload"),
                [
                    new DownloadFile(
                        GameDownload.DlSourceLauncherOrMetaGet(jsonUrl ?? ""),
                        Path.Combine(instanceFolder, $"{instanceName}.json"),
                        new FileChecker(canUseExistsFile: false, isJson: true)
                    )
                ]
            )
            {
                ProgressWeight = 3d,
                Identifier = McDownloadClientJsonId
            }
        );

        var loadersLib = new List<Loading.LoaderBase>
        {
            new Loading.LoaderTask<string, List<DownloadFile>>(
                Lang.Text("Minecraft.Download.Stage.AnalyzeVanillaLibraries.Side"),
                task =>
                {
                    var jsonPath = Path.Combine(instanceFolder, $"{instanceName}.json");
                    WaitForFileReady(jsonPath);
                    LauncherCommon.Log($"[Download] 开始分析原版支持库文件：{instanceFolder}");
                    if (id == "1.16.5" && Config.Download.FixAuthLib)
                        try
                        {
                            var json = LauncherCommon.ReadFile(jsonPath);
                            json = json
                                .Replace("2.1.28/authlib-2.1.28.jar", "2.3.31/authlib-2.3.31.jar")
                                .Replace("com.mojang:authlib:2.1.28", "com.mojang:authlib:2.3.31")
                                .Replace(
                                    "ad54da276bf59983d02d5ed16fc14541354c71fd",
                                    "bbd00ca33b052f73a6312254780fc580d2da3535")
                                .Replace("76328", "87662");
                            LauncherCommon.WriteFile(jsonPath, json);
                        }
                        catch (Exception ex)
                        {
                            LauncherCommon.Log($"[Download] 替换 Authlib 版本失败: {ex.Message}");
                        }

                    task.output = Libraries.McLibNetFilesFromInstance(new McInstance(instanceFolder));
                }
            )
            {
                ProgressWeight = 1d,
                show = false
            },
            new LoaderDownload(
                Lang.Text("Minecraft.Download.Stage.DownloadVanillaLibraries.Side"),
                [])
            {
                ProgressWeight = 13d,
                show = false
            }
        };
        loaders.Add(
            new Loading.LoaderCombo<string>(
                Lang.Text("Minecraft.Download.Stage.VanillaLibrariesDownload"),
                loadersLib)
            {
                block = false,
                ProgressWeight = 14d,
                Identifier = McDownloadClientLibId
            });

        var loadersAssets = new List<Loading.LoaderBase>
        {
            new Loading.LoaderTask<string, List<DownloadFile>>(
                Lang.Text("Minecraft.Download.Stage.AnalyzeAssetsIndex.Side"),
                task =>
                {
                    WaitForFileReady(Path.Combine(instanceFolder, $"{instanceName}.json"));
                    try
                    {
                        var assetIndex = new McInstance(instanceFolder);
                        task.output = GameDownload.DlClientAssetIndexGet(assetIndex) is { } assetIndexFile
                            ? [assetIndexFile]
                            : [];
                    }
                    catch (Exception ex)
                    {
                        throw new Exception(
                            Lang.Text("Minecraft.Download.Error.AssetIndexAnalysisFailed"),
                            ex);
                    }

                    try
                    {
                        var versionJson = (JsonObject)LauncherCommon.GetJson(
                            LauncherCommon.ReadFile(Path.Combine(instanceFolder, $"{instanceName}.json")));
                        versionJson.Add("clientVersion", id);
                        LauncherCommon.WriteFile(
                            Path.Combine(instanceFolder, $"{instanceName}.json"),
                            versionJson.ToString());
                    }
                    catch (Exception ex)
                    {
                        throw new Exception(
                            Lang.Text("Minecraft.Download.Error.AddClientVersionFailed"),
                            ex);
                    }
                }
            )
            {
                ProgressWeight = 1d,
                show = false
            },
            new LoaderDownload(Lang.Text("Minecraft.Download.Stage.DownloadAssetsIndex.Side"), [])
            {
                ProgressWeight = 3d,
                show = false
            },
            new Loading.LoaderTask<string, List<DownloadFile>>(
                Lang.Text("Minecraft.Download.Stage.AnalyzeRequiredAssets.Side"),
                task =>
                {
                    Loading.LoaderBase argprogressFeed = task;
                    task.output = GameAssets.McAssetsFixList(
                        new McInstance(instanceFolder),
                        true,
                        ref argprogressFeed);
                }
            )
            {
                ProgressWeight = 0.01d,
                show = false
            },
            new LoaderDownload(Lang.Text("Minecraft.Download.Stage.DownloadAssets.Side"), [])
            {
                ProgressWeight = 14d,
                show = false
            }
        };
        loaders.Add(
            new Loading.LoaderCombo<string>(
                Lang.Text("Minecraft.Download.Stage.DownloadVanillaAssets"),
                loadersAssets)
            {
                block = false,
                ProgressWeight = 18d
            }
        );

        return loaders;
    }
}
