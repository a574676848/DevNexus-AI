using System.Text.Json;

namespace DevNexus.Client.Shared;

/// <summary>
/// 配置加载器 - 从 appsettings.json 加载配置
/// </summary>
public static class ConfigurationLoader
{
    /// <summary>
    /// 从嵌入资源加载 AppSettings
    /// </summary>
    /// <param name="assembly">程序集</param>
    /// <param name="environment">环境名称（Development/Staging/Production）</param>
    /// <returns>AppSettings 实例</returns>
    public static AppSettings LoadSettings(System.Reflection.Assembly assembly, string? environment = null)
    {
        try
        {
            // 确定配置文件名
            var configFileName = string.IsNullOrEmpty(environment) || environment == "Development"
                ? "appsettings.json"
                : $"appsettings.{environment}.json";

            // 尝试从 wwwroot 加载配置
            var resourceName = $"DevNexus.Client.Shared.wwwroot.{configFileName}";
            using var stream = assembly.GetManifestResourceStream(resourceName);

            if (stream != null)
            {
                using var reader = new StreamReader(stream);
                var json = reader.ReadToEnd();
                var settings = JsonSerializer.Deserialize<AppSettings>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
                if (settings != null)
                {
                    return settings;
                }
            }

            // 回退到默认 appsettings.json
            resourceName = "DevNexus.Client.Shared.wwwroot.appsettings.json";
            using var defaultStream = assembly.GetManifestResourceStream(resourceName);
            if (defaultStream != null)
            {
                using var reader = new StreamReader(defaultStream);
                var json = reader.ReadToEnd();
                var settings = JsonSerializer.Deserialize<AppSettings>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
                if (settings != null)
                {
                    return settings;
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ConfigurationLoader] Failed to load settings: {ex.Message}");
        }

        // 返回默认配置
        return new AppSettings();
    }

    /// <summary>
    /// 从 WebAssembly 主机环境加载 AppSettings
    /// </summary>
    /// <param name="httpClientFactory">HttpClient 工厂</param>
    /// <returns>AppSettings 实例</returns>
    public static async Task<AppSettings> LoadSettingsAsync(IHttpClientFactory httpClientFactory)
    {
        try
        {
            var httpClient = httpClientFactory.CreateClient();
            var response = await httpClient.GetAsync("appsettings.json");
            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                var settings = JsonSerializer.Deserialize<AppSettings>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
                if (settings != null)
                {
                    return settings;
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ConfigurationLoader] Failed to load settings: {ex.Message}");
        }

        return new AppSettings();
    }
}
