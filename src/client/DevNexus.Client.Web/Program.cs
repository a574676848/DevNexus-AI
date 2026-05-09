using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.AspNetCore.Components.Authorization;
using Blazored.LocalStorage;
using DevNexus.Client.Web.Services;
using DevNexus.Client.Shared;
using DevNexus.Client.Shared.Abstractions;
using MudBlazor.Services;
var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<DevNexus.Client.Shared.App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// 加载应用程序配置（支持多环境与 Aspire）
var settings = await LoadSettingsAsync(builder);
builder.Services.AddSingleton(settings);

// 注册共享核心服务
builder.Services.AddSharedCoreServices(settings);

// 注册平台特定服务
ConfigurePlatformServices(builder.Services);

await builder.Build().RunAsync();

/// <summary>
/// 配置 Web 平台特定服务
/// </summary>
static void ConfigurePlatformServices(IServiceCollection services)
{
    // 注册 Blazored LocalStorage
    services.AddBlazoredLocalStorage();
    services.AddMudServices();
    services.AddSingleton<IClientEnvironmentService, WebClientEnvironmentService>();

    // 窗口服务
    services.AddScoped<IWindowService, WebWindowService>();
    services.AddScoped<ISystemCapabilityDetector, WebSystemCapabilityDetector>();

    // 性能监控
    services.AddScoped<IPerformanceMonitor, WebPerformanceMonitor>();

    // 存储服务
    services.AddScoped<IStorageService, WebStorageService>();
    // Scoped: uses IJSRuntime; avoid singleton depending on scoped runtime services.
    services.AddScoped<ISecureStorageService, WebSecureStorageService>();
    services.AddScoped<IFileService, WebFileService>();

    // 系统服务
    services.AddScoped<INotificationService, WebNotificationService>();
    services.AddSingleton<IClientVersionService, WebClientVersionService>();
    services.AddScoped<IUpdateService, WebUpdateService>();
}

/// <summary>
/// 加载配置，支持 appsettings.json、环境配置以及 Aspire 服务发现
/// </summary>
static async Task<AppSettings> LoadSettingsAsync(WebAssemblyHostBuilder builder)
{
    var settings = new AppSettings();
    try
    {
        // 1. 基本环境判断
#if DEBUG
        var environment = "Development";
#else
        var environment = "Production";
#endif

        var httpClient = new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) };

        // 2. 加载基础 appsettings.json
        try
        {
            var response = await httpClient.GetAsync("appsettings.json");
            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                var baseSettings = System.Text.Json.JsonSerializer.Deserialize<AppSettings>(json, new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (baseSettings != null) settings = baseSettings;
            }
        }
        catch { /* 忽略 */ }

        // 3. 处理 Aspire 服务发现 (优先)
        // Aspire 在 WASM 中会通过注入的配置或环境变量提供服务地址
        // 通常格式为 services__apiservice__http__0 或类似的配置项
        var aspireApiUrl = builder.Configuration["services:apiservice:https:0"] 
                        ?? builder.Configuration["services:apiservice:http:0"]
                        ?? builder.Configuration["services:apiservice:default"];

        if (!string.IsNullOrEmpty(aspireApiUrl))
        {
            settings.ApiBaseUrl = aspireApiUrl.TrimEnd('/');
            settings.SignalRHubUrl = $"{settings.ApiBaseUrl}/chat-hub";
            Console.WriteLine($"[Aspire] Service discovery found ApiService at: {settings.ApiBaseUrl}");
            return settings;
        }

        // 4. 加载环境特定 appsettings.{env}.json
        if (environment != "Development")
        {
            try
            {
                var envResponse = await httpClient.GetAsync($"appsettings.{environment}.json");
                if (envResponse.IsSuccessStatusCode)
                {
                    var json = await envResponse.Content.ReadAsStringAsync();
                    var envSettings = System.Text.Json.JsonSerializer.Deserialize<AppSettings>(json, new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    if (envSettings != null) return envSettings;
                }
            }
            catch { /* 忽略 */ }
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[Configuration] Error loading settings: {ex.Message}");
    }

    return settings;
}
