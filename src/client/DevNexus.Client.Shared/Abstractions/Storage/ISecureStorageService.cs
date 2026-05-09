namespace DevNexus.Client.Shared.Abstractions;

/// <summary>
/// 安全存储服务接口 - 提供密钥对存储能力
/// </summary>
public interface ISecureStorageService
{
    Task<string?> GetAsync(string key);
    Task SetAsync(string key, string value);
    Task RemoveAsync(string key);
}

