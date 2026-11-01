using System.Runtime.CompilerServices;
using System.Text;

namespace PCL.Core.App;

/// <summary>
///     程序集级编码初始化：确保 GB2312/GB18030 等区域编码在任何静态成员使用前可用。<br/>
///     .NET 默认仅提供有限编码集；此前依赖 <see cref="StartupService"/> 或宿主程序注册，
///     在测试宿主与早期静态初始化路径下会遗漏。
/// </summary>
internal static class AppEncodingInit
{
    [ModuleInitializer]
    internal static void RegisterEncodings()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }
}
