using System.Collections.Concurrent;
using System.Net.Http.Json;
using System.Runtime.InteropServices;
using DevNexus.Client.Shared.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using DevNexus.Shared.DTOs;

namespace DevNexus.Client.Shared.Services.Logging;

/// <summary>
/// 远程日志服务实现
/// 负责将客户端异常批量上报到服务端
/// </summary>
public class RemoteLogService : IRemoteLogService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IServiceProvider _serviceProvider;

    /// <summary>
    /// 日志队列（线程安全）
    /// </summary>
    private readonly ConcurrentQueue<ClientLogEntryDto> _logQueue = new();

    /// <summary>
    /// 定时刷新计时器
    /// </summary>
    private readonly Timer _flushTimer;

    /// <summary>
    /// 最大批量大小
    /// </summary>
    private const int MaxBatchSize = 50;

    /// <summary>
    /// 刷新间隔（毫秒）
    /// </summary>
    private const int FlushIntervalMs = 5000;

    /// <summary>
    /// 是否已释放
    /// </summary>
    private bool _disposed;

    /// <summary>
    /// 刷新锁（防止并发刷新）
    /// </summary>
    private readonly SemaphoreSlim _flushLock = new(1, 1);

    public RemoteLogService(
        IHttpClientFactory httpClientFactory,
        IServiceProvider serviceProvider)
    {
        _httpClientFactory = httpClientFactory;
        _serviceProvider = serviceProvider;

        // 启动定时刷新（每 5 秒）
        _flushTimer = new Timer(
            async _ => await FlushInternalAsync(),
            null,
            TimeSpan.FromMilliseconds(FlushIntervalMs),
            TimeSpan.FromMilliseconds(FlushIntervalMs));
    }

    /// <inheritdoc />
    public Task LogErrorAsync(Exception exception, string source,
        Dictionary<string, object?>? additionalData = null)
    {
        return EnqueueLogAsync("Error", exception.Message, exception.ToString(), source, additionalData);
    }

    /// <inheritdoc />
    public Task LogWarningAsync(string message, string source,
        Dictionary<string, object?>? additionalData = null)
    {
        return EnqueueLogAsync("Warning", message, null, source, additionalData);
    }

    /// <summary>
    /// 将日志条目加入队列
    /// </summary>
    private Task EnqueueLogAsync(string level, string message, string? exception,
        string source, Dictionary<string, object?>? additionalData)
    {
        // 使用 CreateScope() 来正确地获取作用域服务
        IUserStateService? userState = null;
        try
        {
            using (var scope = _serviceProvider.CreateScope())
            {
                userState = scope.ServiceProvider.GetService<IUserStateService>();
            }
        }
        catch
        {
            // 如果无法获取 IUserStateService，继续处理（不会因此崩溃）
        }

        var entry = new ClientLogEntryDto
        {
            Timestamp = DateTimeOffset.UtcNow,
            Level = level,
            Message = message,
            Exception = exception,
            Source = source,
            UserId = userState?.CurrentUser?.Id,
            ClientVersion = GetClientVersion(),
            Platform = GetPlatform(),
            DeviceModel = GetDeviceModel(),
            OsVersion = GetOsVersion(),
            AdditionalData = additionalData
        };

        _logQueue.Enqueue(entry);

        // 如果队列达到阈值，立即触发刷新
        if (_logQueue.Count >= MaxBatchSize)
        {
            _ = FlushInternalAsync();
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task FlushAsync()
    {
        await FlushInternalAsync();
    }

    /// <summary>
    /// 内部刷新方法（带锁保护）
    /// </summary>
    private async Task FlushInternalAsync()
    {
        if (_disposed || _logQueue.IsEmpty)
        {
            return;
        }

        // 获取锁，避免并发刷新
        if (!await _flushLock.WaitAsync(0))
        {
            return; // 已有刷新任务在执行
        }

        try
        {
            var logs = new List<ClientLogEntryDto>();

            // 从队列中取出最多 MaxBatchSize 条日志
            while (_logQueue.TryDequeue(out var log) && logs.Count < MaxBatchSize)
            {
                logs.Add(log);
            }

            if (logs.Count == 0)
            {
                return;
            }

            await SendLogsAsync(logs);
        }
        finally
        {
            _flushLock.Release();
        }
    }

    /// <summary>
    /// 发送日志到服务端
    /// </summary>
    private async Task SendLogsAsync(List<ClientLogEntryDto> logs)
    {
        try
        {
            // 使用 AuthApi 客户端（避免触发认证逻辑的循环）
            var client = _httpClientFactory.CreateClient("AuthApi");
            var response = await client.PostAsJsonAsync("/api/v1/system/client-logs", logs);

            if (!response.IsSuccessStatusCode)
            {

            }
            else
            {

            }
        }
        catch (Exception)
        {

        }
    }

    /// <summary>
    /// 获取客户端版本
    /// </summary>
    private string? GetClientVersion()
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var clientVersionService = scope.ServiceProvider.GetService<IClientVersionService>();
            if (clientVersionService != null)
            {
                return clientVersionService.CurrentVersion;
            }

            var assembly = typeof(RemoteLogService).Assembly;
            return assembly.GetName().Version?.ToString() ?? "1.0.0";
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// 获取平台信息
    /// </summary>
    private static string? GetPlatform()
    {
        try
        {
            // 使用 RuntimeInformation 替代 DeviceInfo.Platform
            return RuntimeInformation.OSDescription;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// 获取设备型号
    /// </summary>
    private static string? GetDeviceModel()
    {
        try
        {
            // Web 环境下返回浏览器信息
            return RuntimeInformation.OSArchitecture.ToString();
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// 获取操作系统版本
    /// </summary>
    private static string? GetOsVersion()
    {
        try
        {
            // 使用 RuntimeInformation 替代 DeviceInfo.Version
            return RuntimeInformation.OSDescription;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// 释放资源
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        // 停止定时器
        _flushTimer.Dispose();

        // 最后一次刷新
        FlushInternalAsync().GetAwaiter().GetResult();

        _flushLock.Dispose();
    }
}
