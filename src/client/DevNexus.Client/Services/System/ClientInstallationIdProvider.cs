using DevNexus.Client.Shared.Abstractions;

namespace DevNexus.Client.Services.System;

/// <summary>
/// 基于安全存储的安装标识提供器。
/// </summary>
public sealed class ClientInstallationIdProvider : IClientInstallationIdProvider
{
    private const string InstallationIdKey = "DevNexus.Update.InstallationId";
    private readonly ISecureStorageService _secureStorageService;

    /// <summary>
    /// 构造函数。
    /// </summary>
    public ClientInstallationIdProvider(ISecureStorageService secureStorageService)
    {
        _secureStorageService = secureStorageService;
    }

    /// <inheritdoc />
    public async Task<string> GetInstallationIdAsync()
    {
        var existing = await _secureStorageService.GetAsync(InstallationIdKey);
        if (!string.IsNullOrWhiteSpace(existing))
        {
            return existing;
        }

        var generated = Guid.NewGuid().ToString("N");
        await _secureStorageService.SetAsync(InstallationIdKey, generated);
        return generated;
    }
}
