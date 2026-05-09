using System.Text.Json;
using DevNexus.Client.Shared.Abstractions;

namespace DevNexus.Client.Services.System;

/// <summary>
/// 基于安全存储的更新执行状态存储实现。
/// </summary>
public sealed class UpdateStateStore : IUpdateStateStore
{
    private const string SnapshotKey = "DevNexus.Update.ExecutionSnapshot";
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly ISecureStorageService _secureStorageService;

    /// <summary>
    /// 构造函数。
    /// </summary>
    public UpdateStateStore(ISecureStorageService secureStorageService)
    {
        _secureStorageService = secureStorageService;
    }

    /// <inheritdoc />
    public async Task<UpdateExecutionSnapshot?> GetAsync()
    {
        var payload = await _secureStorageService.GetAsync(SnapshotKey);
        if (string.IsNullOrWhiteSpace(payload))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<UpdateExecutionSnapshot>(payload, SerializerOptions);
        }
        catch
        {
            _ = _secureStorageService.RemoveAsync(SnapshotKey);
            return null;
        }
    }

    /// <inheritdoc />
    public Task SaveAsync(UpdateExecutionSnapshot snapshot)
    {
        snapshot.UpdatedAtUtc = DateTime.UtcNow;
        var payload = JsonSerializer.Serialize(snapshot, SerializerOptions);
        return _secureStorageService.SetAsync(SnapshotKey, payload);
    }

    /// <inheritdoc />
    public Task ClearAsync()
    {
        return _secureStorageService.RemoveAsync(SnapshotKey);
    }
}
