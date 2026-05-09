using DevNexus.Client.Shared.Abstractions;
using Microsoft.Maui.Storage;

namespace DevNexus.Client.Services.Storage;

public class MauiSecureStorageService : ISecureStorageService
{
    public async Task<string?> GetAsync(string key)
    {
        return await SecureStorage.Default.GetAsync(key);
    }

    public async Task SetAsync(string key, string value)
    {
        await SecureStorage.Default.SetAsync(key, value);
    }

    public Task RemoveAsync(string key)
    {
        SecureStorage.Default.Remove(key);
        return Task.CompletedTask;
    }
}
