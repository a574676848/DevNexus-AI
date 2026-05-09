using DevNexus.Client.Services.Platform;
using DevNexus.Client.Services.Storage;
using DevNexus.Client.Services.System;
using DevNexus.Client.Shared.Abstractions;
using DevNexus.Client.Shared;
using DevNexus.Client.Shared.Services.Api;
using DevNexus.Client.Shared.Services.Auth;
using DevNexus.Client.Shared.Services.Communication;
using DevNexus.Client.Shared.Services.Http;
using DevNexus.Client.Shared.Services.Session;
using DevNexus.Client.Shared.Services.State;
using DevNexus.Client.Shared.Services.UI;
using Microsoft.AspNetCore.Components.Authorization;
using MudBlazor.Services;

namespace DevNexus.Client;

/// <summary>
/// MAUI 应用程序入口配置
/// </summary>
public static class MauiProgram
{
    /// <summary>
    /// 应用程序设置（静态，供非 DI 场景使用）
    /// </summary>
    public static Shared.AppSettings Settings { get; private set; } = new();

    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

        // Blazor WebView
        builder.Services.AddMauiBlazorWebView();
        builder.Services.AddMudServices();

#if DEBUG
        builder.Services.AddBlazorWebViewDeveloperTools();
#endif

        // 加载配置
        LoadSettings();

        // 注册共享核心服务
        builder.Services.AddSharedCoreServices(Settings);

        // 注册平台特定服务
        ConfigurePlatformServices(builder.Services);

        var app = builder.Build();

        // 配置全局异常处理器（需要在 Build 后获取服务）
        ConfigureGlobalExceptionHandlers(app.Services);

        return app;
    }

    /// <summary>
    /// 配置全局异常处理器
    /// </summary>
    private static void ConfigureGlobalExceptionHandlers(IServiceProvider services)
    {
        // 获取远程日志服务（延迟获取，避免循环依赖）
        IRemoteLogService? GetRemoteLogService()
        {
            try
            {
                return services.GetService<IRemoteLogService>();
            }
            catch
            {
                return null;
            }
        }

        // AppDomain 未处理异常
        AppDomain.CurrentDomain.UnhandledException += (sender, args) =>
        {
            if (args.ExceptionObject is Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[UnhandledException] {ex}");
                try
                {
                    GetRemoteLogService()?.LogErrorAsync(ex, "UnhandledException").GetAwaiter().GetResult();
                }
                catch
                {
                    // 上报失败时忽略，避免无限循环
                }
            }
        };

        // TaskScheduler 未观察异常
        TaskScheduler.UnobservedTaskException += (sender, args) =>
        {
            var exception = args.Exception;
            
            // 忽略 WebView2 中因状态不正确（如关闭期间）导致的 COMException (0x8007139F)
            if (exception != null && exception.InnerExceptions.Any(e => e is System.Runtime.InteropServices.COMException comEx && comEx.ErrorCode == unchecked((int)0x8007139F)))
            {
                args.SetObserved();
                return;
            }

            System.Diagnostics.Debug.WriteLine($"[UnobservedTaskException] {exception}");
            try
            {
                if (exception != null)
                {
                    GetRemoteLogService()?.LogErrorAsync(exception, "UnobservedTaskException").GetAwaiter().GetResult();
                }
            }
            catch
            {
                // 上报失败时忽略
            }
            args.SetObserved(); // 防止进程崩溃
        };
    }

    /// <summary>
    /// 加载应用程序配置（支持多环境）
    /// </summary>
    private static void LoadSettings()
    {
        try
        {
            // 确定环境名称
#if DEBUG
            var environment = "Development";
#else
            var environment = "Production";
#endif
            // 从嵌入资源加载配置
            var assembly = typeof(MauiProgram).Assembly;
            Settings = ConfigurationLoader.LoadSettings(assembly, environment);

            System.Diagnostics.Debug.WriteLine($"[Configuration] Loaded settings from {environment}: {Settings.ApiBaseUrl}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Configuration] Failed to load settings: {ex.Message}");
            // 使用默认值
            Settings = new Shared.AppSettings();
        }
    }

    /// <summary>
    /// 配置 MAUI 平台特定服务
    /// </summary>
    private static void ConfigurePlatformServices(IServiceCollection services)
    {
        services.AddSingleton<IClientEnvironmentService, MauiClientEnvironmentService>();

        // 窗口服务（MAUI 原生实现）
        services.AddSingleton<IWindowService, WindowService>();
        services.AddSingleton<ISystemCapabilityDetector, SystemCapabilityDetector>();

        // 性能监控
        services.AddSingleton<IPerformanceMonitor, PerformanceMonitor>();
        services.Configure<PerformanceOptions>(options =>
        {
            options.EnableVirtualization = true;
            options.EnableImageLazyLoading = true;
            options.EnableComponentCaching = true;
            options.SignalRBatchSize = 10;
            options.SignalRBatchDelayMs = 50;
            options.MarkdownDebounceMs = 100;
            options.EnablePerformanceMonitoring = true;
        });

        // 存储服务
        services.AddSingleton<IStorageService, SqliteStorageService>();
        services.AddSingleton<ISecureStorageService, MauiSecureStorageService>();
        services.AddSingleton<IFileService, FileService>();

        // 更新服务
        services.AddSingleton<IClientVersionService, MauiClientVersionService>();
        ConfigureUpdateServices(services);
        services.AddHostedService<UpdateBackgroundCheckService>();

        // 通知服务
        services.AddSingleton<INotificationService, NotificationService>();

        // 更新配置
        services.Configure<UpdateOptions>(options =>
        {
            options.CheckIntervalHours = 24;
            options.AutoDownload = false;
            options.AutoInstall = false;
        });
    }

    private static void ConfigureUpdateServices(IServiceCollection services)
    {
        services.AddSingleton<IClientInstallationIdProvider, ClientInstallationIdProvider>();
        services.AddSingleton<IUpdatePreferenceStore, UpdatePreferenceStore>();
        services.AddSingleton<IUpdatePackageExecutor, UpdatePackageExecutor>();

#if WINDOWS
        services.AddSingleton<IUpdateInstallResultStore, UpdateInstallResultStore>();
        services.AddSingleton<IUpdateStateStore, UpdateStateStore>();
        services.AddSingleton<IUpdateInstallerLauncher, UpdateInstallerLauncher>();
        services.AddSingleton<IUpdateCoordinator, UpdateCoordinator>();
        services.AddSingleton<IUpdateService, UpdateService>();
#elif MACCATALYST
        services.AddSingleton<IUpdateInstallerLauncher, MacUpdateInstallerLauncher>();
        services.AddSingleton<IUpdateService, ManualDesktopUpdateService>();
#else
        services.AddSingleton<IUpdateService, ManualDesktopUpdateService>();
#endif
    }
}
