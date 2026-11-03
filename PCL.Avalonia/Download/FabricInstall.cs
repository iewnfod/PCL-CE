using System.IO;
using System.Text;
using PCL.Core.App.Localization;
using PCL.Network;
using PCL.Network.Loaders;
using PCL.Text;

namespace PCL.Avalonia;

public static partial class DownloadUi
{
    /// <summary>
    ///     获取下载某个 Fabric 实例的加载器列表（WPF DownloadUi/Fabric.cs 移植；MVP 仅支持 fixLibrary=false 的合并安装路径）。
    /// </summary>
    private static List<Loading.LoaderBase> McDownloadFabricLoader(
        string fabricVersion,
        string minecraftName,
        string? mcFolder = null,
        bool fixLibrary = true
    )
    {
        if (fixLibrary)
            throw new NotSupportedException(
                "Avalonia MVP 仅支持合并安装路径下的 Fabric 下载（fixLibrary=false）");

        mcFolder ??= GameFolders.mcFolderSelected ?? "";
        var id = $"fabric-loader-{fabricVersion}-{minecraftName}";
        var versionFolder = Path.Combine(mcFolder ?? "", "versions", id);
        var loaders = new List<Loading.LoaderBase>();

        minecraftName = minecraftName.Replace("∞", "infinite");
        loaders.Add(
            new Loading.LoaderTask<string, List<DownloadFile>>(
                Lang.Text("Minecraft.Download.Stage.ObtainFabricMainFileUrl"),
                task =>
                {
                    task.Progress = 0.5d;

                    var safeName = minecraftName.Replace("∞", "infinite");
                    var bmclapiUrl =
                        $"https://bmclapi2.bangbang93.com/fabric-meta/v2/versions/loader/{safeName}/{fabricVersion}/profile/json";
                    var officialUrl =
                        $"https://meta.fabricmc.net/v2/versions/loader/{safeName}/{fabricVersion}/profile/json";

                    string? json = null;
                    foreach (var url in new[] { bmclapiUrl, officialUrl })
                        try
                        {
                            json = Requester.FetchString(
                                url,
                                new RequestParam
                                {
                                    UseBrowserUserAgent = true,
                                    Timeout = 5000,
                                    Retries = 2
                                }
                            );
                            if (json is not null)
                                break;
                        }
                        catch (Exception ex)
                        {
                            LauncherCommon.Log(ex, $"[Download] 从 {url} 下载 Fabric meta 失败");
                        }

                    if (json is null)
                        throw new Exception(
                            Lang.Text(
                                "Minecraft.Download.Error.FabricMetaDownloadFailed",
                                $"{bmclapiUrl} and {officialUrl}")
                        );

                    Directory.CreateDirectory(versionFolder);
                    File.WriteAllText(
                        Path.Combine(versionFolder, $"{id}.json"),
                        json,
                        Encoding.UTF8);
                    task.output = [];
                }
            )
            {
                ProgressWeight = 0.5d
            }
        );
        loaders.Add(
            new LoaderDownload(
                Lang.Text("Minecraft.Download.Stage.DownloadLoaderMainFile", "Fabric"),
                [])
            {
                ProgressWeight = 2.5d
            }
        );

        return loaders;
    }
}
