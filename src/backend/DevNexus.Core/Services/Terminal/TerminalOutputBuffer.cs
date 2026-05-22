using System.Collections.Concurrent;
using System.Text;
using DevNexus.Core.Abstractions;
using DevNexus.Domain.Entities;
using DevNexus.Shared.Enums;
using DevNexus.Shared.DTOs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using DevNexus.Shared.Constants;

namespace DevNexus.Core.Services.Terminal;

/// <summary>
/// 终端输出内存缓冲服务实现
/// </summary>
public class TerminalOutputBuffer : ITerminalOutputBuffer, IDisposable
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<TerminalOutputBuffer> _logger;
    private readonly ConcurrentDictionary<Guid, StreamBuffer> _buffers = new();
    private readonly Timer _flushTimer;
    private readonly SemaphoreSlim _flushLock = new(1, 1);

    // 统计信息
    private long _totalFlushCount;
    private long _failedFlushCount;

    // 配置参数
    private const int FlushIntervalMs = 500; // 定时刷新间隔
    private const int FlushThresholdBytes = 1024; // 1KB 阈值
    private const int FlushThresholdSeconds = 1; // 1 秒未更新则刷新

    public TerminalOutputBuffer(
        IServiceScopeFactory scopeFactory,
        ILogger<TerminalOutputBuffer> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;

        // 启动定时刷新任务
        _flushTimer = new Timer(OnTimerTick, null, FlushIntervalMs, FlushIntervalMs);

        _logger.LogInformation(
            "[TerminalOutputBuffer] Initialized | FlushInterval={Interval}ms ThresholdBytes={Bytes} ThresholdSeconds={Seconds}",
            FlushIntervalMs, FlushThresholdBytes, FlushThresholdSeconds);
    }

    /// <inheritdoc />
    public Task AppendAsync(Guid streamId, string outputDelta, Dictionary<string, object>? metadata = null)
    {
        if (streamId == Guid.Empty || string.IsNullOrEmpty(outputDelta))
        {
            return Task.CompletedTask;
        }

        var buffer = _buffers.GetOrAdd(streamId, _ => new StreamBuffer
        {
            StreamId = streamId,
            Metadata = metadata ?? new Dictionary<string, object>()
        });

        lock (buffer.Lock)
        {
            buffer.Output.Append(outputDelta);
            buffer.LastAppendAt = DateTime.UtcNow;
            buffer.PendingLength += outputDelta.Length;
            buffer.TotalLength += outputDelta.Length;
            var newlineCount = TerminalOutputWatchSummaryBuilder.CountNewLines(outputDelta);
            buffer.PendingNewlineCount += newlineCount;
            buffer.TotalNewlineCount += newlineCount;
            buffer.PendingChunkCount++;
            buffer.TotalChunkCount++;
            UpdateWatchLabels(buffer, outputDelta);

            // 更新元数据（如果提供）
            if (metadata != null)
            {
                foreach (var kvp in metadata)
                {
                    buffer.Metadata[kvp.Key] = kvp.Value;
                }
            }
        }

        _logger.LogTrace(
            "[TerminalOutputBuffer] Appended | StreamId={StreamId} DeltaLength={Length} TotalLength={Total}",
            streamId, outputDelta.Length, buffer.Output.Length);

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task<bool> FlushAsync(Guid streamId, CancellationToken cancellationToken = default)
    {
        if (!_buffers.TryGetValue(streamId, out var buffer))
        {
            _logger.LogDebug(
                "[TerminalOutputBuffer] No buffer found for flush | StreamId={StreamId}",
                streamId);
            return false;
        }

        // 防止并发刷新同一个流
        if (!await buffer.FlushLock.WaitAsync(0, cancellationToken))
        {
            _logger.LogTrace(
                "[TerminalOutputBuffer] Flush already in progress | StreamId={StreamId}",
                streamId);
            return false;
        }

        try
        {
            string output;
            Dictionary<string, object> metadata;
            int pendingLength;
            int pendingNewlineCount;
            int pendingChunkCount;
            int totalLength;
            int totalNewlineCount;
            int totalChunkCount;
            string? watchSummary;

            lock (buffer.Lock)
            {
                if (buffer.Output.Length == 0)
                {
                    return false; // 无数据需要刷新
                }

                output = buffer.Output.ToString();
                metadata = new Dictionary<string, object>(buffer.Metadata);
                pendingLength = buffer.PendingLength;
                pendingNewlineCount = buffer.PendingNewlineCount;
                pendingChunkCount = buffer.PendingChunkCount;
                totalLength = buffer.TotalLength;
                totalNewlineCount = buffer.TotalNewlineCount;
                totalChunkCount = buffer.TotalChunkCount;
                watchSummary = TerminalOutputWatchSummaryBuilder.Build(buffer.WatchLabels);

                // 清空缓冲（准备下次累积）
                buffer.Output.Clear();
                buffer.PendingLength = 0;
                buffer.PendingNewlineCount = 0;
                buffer.PendingChunkCount = 0;
            }

            // 持久化到数据库
            await PersistToDatabase(
                streamId,
                output,
                pendingLength,
                pendingNewlineCount,
                pendingChunkCount,
                totalLength,
                totalNewlineCount,
                totalChunkCount,
                watchSummary,
                metadata,
                cancellationToken);

            Interlocked.Increment(ref _totalFlushCount);

            _logger.LogDebug(
                "[TerminalOutputBuffer] Flushed | StreamId={StreamId} OutputLength={Length}",
                streamId, output.Length);

            return true;
        }
        catch (Exception ex)
        {
            Interlocked.Increment(ref _failedFlushCount);

            _logger.LogError(ex,
                "[TerminalOutputBuffer] Flush failed | StreamId={StreamId}",
                streamId);

            return false;
        }
        finally
        {
            buffer.FlushLock.Release();
        }
    }

    /// <inheritdoc />
    public async Task FlushAllAsync(CancellationToken cancellationToken = default)
    {
        var streamIds = _buffers.Keys.ToList();

        _logger.LogDebug(
            "[TerminalOutputBuffer] Flushing all buffers | Count={Count}",
            streamIds.Count);

        foreach (var streamId in streamIds)
        {
            try
            {
                await FlushAsync(streamId, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "[TerminalOutputBuffer] Failed to flush stream | StreamId={StreamId}",
                    streamId);
            }
        }
    }

    /// <inheritdoc />
    public BufferStatistics GetStatistics()
    {
        long totalSize = 0;
        foreach (var buffer in _buffers.Values)
        {
            lock (buffer.Lock)
            {
                totalSize += buffer.Output.Length;
            }
        }

        return new BufferStatistics
        {
            ActiveStreams = _buffers.Count,
            TotalBufferSize = totalSize,
            TotalFlushCount = Interlocked.Read(ref _totalFlushCount),
            FailedFlushCount = Interlocked.Read(ref _failedFlushCount)
        };
    }

    /// <inheritdoc />
    public void Remove(Guid streamId)
    {
        if (_buffers.TryRemove(streamId, out var buffer))
        {
            buffer.FlushLock.Dispose();

            _logger.LogDebug(
                "[TerminalOutputBuffer] Removed buffer | StreamId={StreamId}",
                streamId);
        }
    }

    /// <inheritdoc />
    public async Task<TerminalOutputContentDto?> ReadOutputAsync(
        Guid streamId,
        CancellationToken cancellationToken = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<ITerminalStreamRepository>();
        var stream = await repository.GetByIdAsync(streamId, cancellationToken);
        if (stream == null)
        {
            return null;
        }

        var content = stream.Output ?? string.Empty;
        if (stream.HasArchivedOutput
            && !string.IsNullOrWhiteSpace(stream.ArchivedOutputPath)
            && File.Exists(stream.ArchivedOutputPath))
        {
            content = await File.ReadAllTextAsync(stream.ArchivedOutputPath, cancellationToken);
        }

        return new TerminalOutputContentDto
        {
            RecordId = streamId,
            Content = content,
            HasArchivedOutput = stream.HasArchivedOutput,
            OutputLength = stream.OutputLength,
            OutputLineCount = stream.OutputLineCount,
            WatchSummary = stream.WatchSummary
        };
    }

    /// <summary>
    /// 定时器回调：检查并刷新满足条件的缓冲
    /// </summary>
    private async void OnTimerTick(object? state)
    {
        // 防止并发执行
        if (!await _flushLock.WaitAsync(0))
        {
            return;
        }

        try
        {
            var now = DateTime.UtcNow;
            var streamsToFlush = new List<Guid>();

            foreach (var (streamId, buffer) in _buffers)
            {
                lock (buffer.Lock)
                {
                    var idleSeconds = (now - buffer.LastAppendAt).TotalSeconds;
                    var bufferSize = buffer.Output.Length;

                    // 满足刷新条件：超过阈值大小 或 超过空闲时间
                    if (bufferSize >= FlushThresholdBytes || idleSeconds >= FlushThresholdSeconds)
                    {
                        streamsToFlush.Add(streamId);
                    }
                }
            }

            if (streamsToFlush.Count > 0)
            {
                _logger.LogTrace(
                    "[TerminalOutputBuffer] Timer triggered flush | Count={Count}",
                    streamsToFlush.Count);

                foreach (var streamId in streamsToFlush)
                {
                    await FlushAsync(streamId);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[TerminalOutputBuffer] Timer tick failed");
        }
        finally
        {
            _flushLock.Release();
        }
    }

    /// <summary>
    /// 持久化到数据库
    /// </summary>
    private async Task PersistToDatabase(
        Guid streamId,
        string output,
        int pendingLength,
        int pendingNewlineCount,
        int pendingChunkCount,
        int totalLength,
        int totalNewlineCount,
        int totalChunkCount,
        string? watchSummary,
        Dictionary<string, object> metadata,
        CancellationToken cancellationToken)
    {
        // 使用 IServiceScopeFactory 创建临时 scope 来访问 scoped 服务
        using var scope = _scopeFactory.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<ITerminalStreamRepository>();

        // 检查是否已存在
        var existing = await repository.GetByIdAsync(streamId, cancellationToken);

        if (existing == null)
        {
            var messageId = GetGuidFromMetadata(metadata, "messageId");
            var archivedOutputPath = await AppendArchivedOutputAsync(
                null,
                streamId,
                output,
                metadata,
                cancellationToken);

            // 创建新记录
            var stream = new TerminalStream
            {
                Id = streamId,
                SessionKey = GetStringFromMetadata(metadata, TerminalBlockMetadataKeys.SessionKey),
                ChatSessionId = GetGuidFromMetadata(metadata, TerminalBlockMetadataKeys.ChatSessionId),
                UserId = GetGuidFromMetadata(metadata, TerminalBlockMetadataKeys.UserId),
                MessageId = messageId,
                ToolCallId = GetGuidFromMetadata(metadata, TerminalBlockMetadataKeys.ToolCallId),
                Command = GetStringFromMetadata(metadata, TerminalBlockMetadataKeys.Command) ?? string.Empty,
                WorkingDirectory = GetStringFromMetadata(metadata, TerminalBlockMetadataKeys.WorkingDirectory),
                LockKey = GetStringFromMetadata(metadata, TerminalBlockMetadataKeys.LockKey),
                AttemptNumber = GetIntFromMetadata(metadata, TerminalBlockMetadataKeys.AttemptNumber) ?? 1,
                IsRetry = GetBoolFromMetadata(metadata, TerminalBlockMetadataKeys.IsRetry) ?? false,
                Status = TerminalStreamStatusExtensions.Parse(GetStringFromMetadata(metadata, TerminalBlockMetadataKeys.Status)),
                SessionState = CliSessionStateExtensions.Parse(GetStringFromMetadata(metadata, TerminalBlockMetadataKeys.SessionState)),
                RuntimeHost = GetStringFromMetadata(metadata, TerminalBlockMetadataKeys.RuntimeHost),
                ExitCode = GetIntFromMetadata(metadata, TerminalBlockMetadataKeys.ExitCode),
                StartedAt = GetDateTimeFromMetadata(metadata, TerminalBlockMetadataKeys.StartedAt) ?? DateTime.UtcNow,
                LastActivityAt = GetDateTimeFromMetadata(metadata, TerminalBlockMetadataKeys.LastActivityAt) ?? DateTime.UtcNow,
                WaitingForInput = GetBoolFromMetadata(metadata, TerminalBlockMetadataKeys.WaitingForInput) ?? false,
                WaitingForInputSince = GetDateTimeFromMetadata(metadata, TerminalBlockMetadataKeys.WaitingForInputSince),
                TerminationReason = CliSessionTerminationReasons.Normalize(
                    GetStringFromMetadata(metadata, TerminalBlockMetadataKeys.TerminationReason),
                    string.Empty),
                Output = TerminalArchivedOutputPreview.Normalize(output),
                HasArchivedOutput = !string.IsNullOrWhiteSpace(archivedOutputPath),
                ArchivedOutputPath = archivedOutputPath,
                OutputLength = pendingLength,
                OutputLineCount = pendingLength > 0 ? pendingNewlineCount + 1 : 0,
                OutputChunkCount = pendingChunkCount,
                WatchSummary = watchSummary
            };

            await repository.CreateAsync(stream, cancellationToken);
        }
        else
        {
            var messageId = GetGuidFromMetadata(metadata, TerminalBlockMetadataKeys.MessageId);

            if (!existing.MessageId.HasValue && messageId.HasValue)
            {
                existing.MessageId = messageId.Value;
            }

            // 追加输出
            var archivedOutputPath = await AppendArchivedOutputAsync(
                existing.ArchivedOutputPath,
                streamId,
                output,
                metadata,
                cancellationToken);
            existing.Output = TerminalArchivedOutputPreview.Normalize(
                $"{TerminalArchivedOutputPreview.StripBanner(existing.Output)}{output}");
            var parsedStatus = TerminalStreamStatusExtensions.Parse(GetStringFromMetadata(metadata, TerminalBlockMetadataKeys.Status));
            var parsedSessionState = CliSessionStateExtensions.Parse(GetStringFromMetadata(metadata, TerminalBlockMetadataKeys.SessionState));

            if (parsedStatus != TerminalStreamStatus.Unknown)
            {
                existing.Status = parsedStatus;
            }

            if (parsedSessionState != CliSessionState.Unknown)
            {
                existing.SessionState = parsedSessionState;
            }
            existing.RuntimeHost = GetStringFromMetadata(metadata, TerminalBlockMetadataKeys.RuntimeHost) ?? existing.RuntimeHost;
            existing.ExitCode = GetIntFromMetadata(metadata, TerminalBlockMetadataKeys.ExitCode) ?? existing.ExitCode;
            existing.LastActivityAt = GetDateTimeFromMetadata(metadata, TerminalBlockMetadataKeys.LastActivityAt) ?? existing.LastActivityAt;
            existing.WaitingForInput = GetBoolFromMetadata(metadata, TerminalBlockMetadataKeys.WaitingForInput) ?? existing.WaitingForInput;
            existing.WaitingForInputSince = GetDateTimeFromMetadata(metadata, TerminalBlockMetadataKeys.WaitingForInputSince)
                ?? existing.WaitingForInputSince;
            existing.TerminationReason = CliSessionTerminationReasons.Normalize(
                    GetStringFromMetadata(metadata, TerminalBlockMetadataKeys.TerminationReason),
                    existing.TerminationReason ?? string.Empty)
                ?? existing.TerminationReason;
            existing.HasArchivedOutput = !string.IsNullOrWhiteSpace(archivedOutputPath);
            existing.ArchivedOutputPath = archivedOutputPath;
            existing.OutputLength = Math.Max(existing.OutputLength + pendingLength, totalLength);
            existing.OutputLineCount = existing.OutputLength > 0
                ? Math.Max(existing.OutputLineCount + pendingNewlineCount, totalNewlineCount + 1)
                : 0;
            existing.OutputChunkCount = Math.Max(existing.OutputChunkCount + pendingChunkCount, totalChunkCount);
            existing.WatchSummary = TerminalOutputWatchSummaryBuilder.Merge(existing.WatchSummary, watchSummary);

            await repository.UpdateAsync(existing, cancellationToken);
        }
    }

    private async Task<string?> AppendArchivedOutputAsync(
        string? existingPath,
        Guid streamId,
        string output,
        Dictionary<string, object> metadata,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(output))
        {
            return existingPath;
        }

        var archivePath = existingPath;
        if (string.IsNullOrWhiteSpace(archivePath))
        {
            archivePath = BuildArchivePath(streamId, metadata);
        }

        var directory = Path.GetDirectoryName(archivePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await File.AppendAllTextAsync(archivePath, output, Encoding.UTF8, cancellationToken);
        return archivePath;
    }

    private static string BuildArchivePath(Guid streamId, Dictionary<string, object> metadata)
    {
        var userId = GetGuidFromMetadata(metadata, TerminalBlockMetadataKeys.UserId);
        var sessionId = GetGuidFromMetadata(metadata, TerminalBlockMetadataKeys.ChatSessionId);
        var root = Path.Combine(
            Path.GetTempPath(),
            "DevNexus-AI",
            "terminal-logs",
            userId?.ToString("N") ?? "anonymous",
            sessionId?.ToString("N") ?? "detached");
        return Path.Combine(root, $"{streamId:N}.log");
    }

    private static void UpdateWatchLabels(StreamBuffer buffer, string outputDelta)
    {
        foreach (var label in TerminalOutputWatchSummaryBuilder.DetectLabels(outputDelta))
        {
            buffer.WatchLabels.Add(label);
        }
    }

    /// <summary>
    /// 从元数据中提取 Guid
    /// </summary>
    private static Guid? GetGuidFromMetadata(Dictionary<string, object>? metadata, string key)
    {
        if (metadata == null || !metadata.TryGetValue(key, out var value) || value == null)
        {
            return null;
        }

        if (value is Guid guidValue)
        {
            return guidValue;
        }

        if (Guid.TryParse(value.ToString(), out var parsed))
        {
            return parsed;
        }

        return null;
    }

    /// <summary>
    /// 从元数据中提取字符串
    /// </summary>
    private static string? GetStringFromMetadata(Dictionary<string, object>? metadata, string key)
    {
        if (metadata == null || !metadata.TryGetValue(key, out var value) || value == null)
        {
            return null;
        }

        return value.ToString();
    }

    /// <summary>
    /// 从元数据中提取整数
    /// </summary>
    private static int? GetIntFromMetadata(Dictionary<string, object>? metadata, string key)
    {
        if (metadata == null || !metadata.TryGetValue(key, out var value) || value == null)
        {
            return null;
        }

        if (value is int intValue)
        {
            return intValue;
        }

        if (int.TryParse(value.ToString(), out var parsed))
        {
            return parsed;
        }

        return null;
    }

    /// <summary>
    /// 从元数据中提取布尔值
    /// </summary>
    private static bool? GetBoolFromMetadata(Dictionary<string, object>? metadata, string key)
    {
        if (metadata == null || !metadata.TryGetValue(key, out var value) || value == null)
        {
            return null;
        }

        if (value is bool boolValue)
        {
            return boolValue;
        }

        if (bool.TryParse(value.ToString(), out var parsed))
        {
            return parsed;
        }

        return null;
    }

    /// <summary>
    /// 从元数据中提取时间
    /// </summary>
    private static DateTime? GetDateTimeFromMetadata(Dictionary<string, object>? metadata, string key)
    {
        if (metadata == null || !metadata.TryGetValue(key, out var value) || value == null)
        {
            return null;
        }

        if (value is DateTime dateTimeValue)
        {
            return dateTimeValue;
        }

        if (DateTime.TryParse(value.ToString(), out var parsed))
        {
            return parsed;
        }

        return null;
    }

    public void Dispose()
    {
        _flushTimer?.Dispose();
        _flushLock?.Dispose();

        foreach (var buffer in _buffers.Values)
        {
            buffer.FlushLock.Dispose();
        }

        _buffers.Clear();

        _logger.LogInformation("[TerminalOutputBuffer] Disposed");
    }

    /// <summary>
    /// 流缓冲内部类
    /// </summary>
    private class StreamBuffer
    {
        public Guid StreamId { get; set; }
        public StringBuilder Output { get; } = new();
        public Dictionary<string, object> Metadata { get; set; } = new();
        public DateTime LastAppendAt { get; set; } = DateTime.UtcNow;
        public SemaphoreSlim FlushLock { get; } = new(1, 1);
        public object Lock { get; } = new();
        public int PendingLength { get; set; }
        public int PendingNewlineCount { get; set; }
        public int PendingChunkCount { get; set; }
        public int TotalLength { get; set; }
        public int TotalNewlineCount { get; set; }
        public int TotalChunkCount { get; set; }
        public HashSet<string> WatchLabels { get; } = new(StringComparer.Ordinal);
    }
}
