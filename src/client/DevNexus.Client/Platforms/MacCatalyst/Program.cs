#if MACCATALYST
using ObjCRuntime;
using UIKit;

namespace DevNexus.Client.MacCatalyst;

/// <summary>
/// Mac Catalyst 应用程序入口点。
/// </summary>
public class Program
{
    static void Main(string[] args)
    {
        UIApplication.Main(args, null, typeof(AppDelegate));
    }
}
#endif
