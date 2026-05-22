#if MACCATALYST
using Foundation;
using Microsoft.Maui;
using Microsoft.Maui.Hosting;

namespace DevNexus.Client.MacCatalyst;

/// <summary>
/// Mac Catalyst 应用委托，作为 MAUI 应用的入口点。
/// </summary>
[Register("AppDelegate")]
public class AppDelegate : MauiUIApplicationDelegate
{
    protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
}
#endif
