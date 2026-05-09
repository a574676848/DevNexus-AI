using System.Text.Json;

namespace DevNexus.Client.Services.System;

/// <summary>
/// 基于本地文件的安装结果存储。
/// </summary>
public sealed class UpdateInstallResultStore : IUpdateInstallResultStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);
    private readonly string _resultFilePath;

    /// <summary>
    /// 构造函数。
    /// </summary>
    public UpdateInstallResultStore()
    {
        _resultFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "DevNexus",
            "Updates",
            "install-result.json");
    }

    /// <inheritdoc />
    public async Task<UpdateInstallResult?> GetAsync()
    {
        if (!File.Exists(_resultFilePath))
        {
            return null;
        }

        try
        {
            var payload = await File.ReadAllTextAsync(_resultFilePath);
            return JsonSerializer.Deserialize<UpdateInstallResult>(payload, SerializerOptions);
        }
        catch
        {
            return null;
        }
    }

    /// <inheritdoc />
    public Task ClearAsync()
    {
        if (File.Exists(_resultFilePath))
        {
            File.Delete(_resultFilePath);
        }

        return Task.CompletedTask;
    }
}
