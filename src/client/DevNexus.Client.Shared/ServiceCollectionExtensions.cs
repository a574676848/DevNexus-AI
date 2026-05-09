using DevNexus.Client.Shared.Abstractions.Chat;
using DevNexus.Client.Shared.Services.Api;
using DevNexus.Client.Shared.Services.Auth;
using DevNexus.Client.Shared.Services.Chat;
using DevNexus.Client.Shared.Services.Communication;
using DevNexus.Client.Shared.Services.Http;
using DevNexus.Client.Shared.Services.Logging;
using DevNexus.Client.Shared.Services.Session;
using DevNexus.Client.Shared.Services.State;
using DevNexus.Client.Shared.Services.Storage;
using DevNexus.Client.Shared.Services.System;
using DevNexus.Client.Shared.Services.UI;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;

namespace DevNexus.Client.Shared;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddSharedCoreServices(this IServiceCollection services, AppSettings settings)
    {
        // 核心配置
        services.AddSingleton(settings);

        // HTTP Handlers & Clients
        services.AddTransient<AuthorizationHandler>();
        services.AddTransient<LoggingHandler>();

        services.AddHttpClient("AuthApi", client =>
        {
            client.BaseAddress = new Uri(settings.ApiBaseUrl);
            client.DefaultRequestHeaders.Add("Accept", "application/json");
            client.Timeout = TimeSpan.FromSeconds(30);
        })
        .AddHttpMessageHandler<LoggingHandler>();

        services.AddHttpClient("DevNexusApi", client =>
        {
            client.BaseAddress = new Uri(settings.ApiBaseUrl);
            client.DefaultRequestHeaders.Add("Accept", "application/json");
            client.Timeout = TimeSpan.FromSeconds(30);
        })
        .AddHttpMessageHandler<LoggingHandler>()
        .AddHttpMessageHandler<AuthorizationHandler>();

        services.AddHttpClient("DirectUpload", client =>
        {
            client.Timeout = TimeSpan.FromMinutes(5);
        });

        // 状态管理
        services.AddSingleton<IChatState, ChatState>();
        services.AddSingleton<ISessionState, SessionState>();
        services.AddScoped<IUserStateService, UserStateService>();

        // 认证与授权
        // In Blazor WebAssembly, Scoped services are effectively singletons for the app host.
        // In Blazor Hybrid / Server, Scoped has real scope semantics (WebView/Circuit).
        // Use Scoped here to avoid singleton depending on JSRuntime-backed services.
        services.AddSingleton<AuthRuntimeState>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddAuthorizationCore();
        services.AddScoped<AuthenticationStateProvider, DevNexusAuthenticationStateProvider>();

        // 基础架构服务
        services.AddScoped<ISignalRService, SignalRService>();
        services.AddScoped<ApiService>();
        services.AddScoped<IApiService>(sp => sp.GetRequiredService<ApiService>());
        services.AddScoped<ISystemApiService>(sp => sp.GetRequiredService<ApiService>());
        services.AddScoped<IArtifactApiService>(sp => sp.GetRequiredService<ApiService>());
        services.AddScoped<IFilePlatformApiService>(sp => sp.GetRequiredService<ApiService>());
        services.AddScoped<IMemoryApiService>(sp => sp.GetRequiredService<ApiService>());
        services.AddScoped<ISkillApiService>(sp => sp.GetRequiredService<ApiService>());
        services.AddScoped<INoteProviderApiService>(sp => sp.GetRequiredService<ApiService>());
        services.AddScoped<INoteApiService>(sp => sp.GetRequiredService<ApiService>());
        services.AddScoped<IUserIntegrationApiService>(sp => sp.GetRequiredService<ApiService>());
        services.AddScoped<ISessionManager, SessionManager>();
        services.AddSingleton<IRemoteLogService, RemoteLogService>();
        services.AddSingleton<IErrorTranslationService, ErrorTranslationService>();

        // UI 与 交互辅助
        services.AddScoped<IThemeService, ThemeService>();
        services.AddSingleton<IUrlService, UrlService>();
        services.AddSingleton<ILogoService, LogoService>();
        services.AddSingleton<IImagePreviewService, ImagePreviewService>();
        services.AddSingleton<ToastService>();
        services.AddSingleton<FormDraftService>();

        // 业务处理逻辑
        services.AddScoped<FileUploadService>();
        services.AddScoped<ChatArtifactTracker>();
        services.AddScoped<ChatBlockCollectionService>();
        services.AddScoped<ChatArtifactPersistenceService>();
        services.AddScoped<IChatMessageProcessor, ChatMessageProcessor>();
        services.AddScoped<IMessageHandlingService, MessageHandlingService>();
        services.AddScoped<IMessageEditingService, MessageEditingService>();
        services.AddScoped<IGenerationControlService, GenerationControlService>();
        services.AddScoped<IStateSynchronizationService, StateSynchronizationService>();
        services.AddSingleton<IComposerFileBridgeService, ComposerFileBridgeService>();

        return services;
    }
}
