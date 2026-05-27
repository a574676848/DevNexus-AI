using System;
using System.Text;
using System.Threading.Channels;
using DevNexus.Shared.DTOs;

/// <summary>
/// Channel Writer 包装器，用于拦截并累积 Thinking 块内容
/// 支持周期性持久化功能，保护长时间运行的任务（如 Swarm）
/// </summary>
/// <remarks>
/// 设计目标：在不修改 SwarmOrchestrator 等组件的情况下，
/// 透明地累积所有推送的思维链内容，用于后续持久化到数据库
/// </remarks>
public class ThinkingAccumulatingChannelWriter : ChannelWriter<BlockDto>
{
    private readonly ChannelWriter<BlockDto> _innerWriter;
    private readonly StringBuilder _thinkingAccumulator;
    private readonly object _thinkingLock = new();
    
    // ★ 周期性持久化配置
    private int _thinkingBlockCount = 0;
    private const int PERSISTENCE_THRESHOLD = 3;  // 每 3 个 Thinking Block 触发一次持久化
    private Func<string, Task>? _persistenceCallback;  // 异步持久化回调

    public ThinkingAccumulatingChannelWriter(
        ChannelWriter<BlockDto> innerWriter,
        StringBuilder thinkingAccumulator)
    {
        _innerWriter = innerWriter;
        _thinkingAccumulator = thinkingAccumulator;
    }
    
    /// <summary>
    /// 设置周期性持久化回调
    /// </summary>
    public void SetPersistenceCallback(Func<string, Task>? callback)
    {
        lock (_thinkingLock)
        {
            _persistenceCallback = callback;
        }
    }

    public override bool TryWrite(BlockDto item)
    {
        AccumulateThinking(item);

        // 委托给实际的 writer
        return _innerWriter.TryWrite(item);
    }

    public override ValueTask<bool> WaitToWriteAsync(CancellationToken cancellationToken = default)
    {
        return _innerWriter.WaitToWriteAsync(cancellationToken);
    }

    public override ValueTask WriteAsync(BlockDto item, CancellationToken cancellationToken = default)
    {
        AccumulateThinking(item);

        // 委托给实际的 writer
        return _innerWriter.WriteAsync(item, cancellationToken);
    }

    public override bool TryComplete(Exception? error = null)
    {
        return _innerWriter.TryComplete(error);
    }

    public string SnapshotThinkingContent()
    {
        lock (_thinkingLock)
        {
            return _thinkingAccumulator.ToString();
        }
    }
    
    /// <summary>
    /// 触发异步持久化（火即忘，不阻塞流式输出）
    /// </summary>
    private void AccumulateThinking(BlockDto item)
    {
        if (item.BlockType != BlockType.Thinking || string.IsNullOrEmpty(item.Content))
        {
            return;
        }

        string? partialThinking = null;
        Func<string, Task>? persistenceCallback = null;

        lock (_thinkingLock)
        {
            _thinkingAccumulator.Append(item.Content);
            _thinkingBlockCount++;

            if (_thinkingBlockCount < PERSISTENCE_THRESHOLD || _persistenceCallback == null)
            {
                return;
            }

            partialThinking = DrainThinkingAccumulatorCore();
            persistenceCallback = _persistenceCallback;
        }

        TriggerPersistenceAsync(partialThinking, persistenceCallback);
    }

    private string DrainThinkingAccumulatorCore()
    {
        var content = _thinkingAccumulator.ToString();
        _thinkingAccumulator.Clear();
        _thinkingBlockCount = 0;
        return content;
    }

    private static void TriggerPersistenceAsync(string content, Func<string, Task>? persistenceCallback)
    {
        if (persistenceCallback == null || string.IsNullOrEmpty(content)) return;
        
        _ = Task.Run(async () =>
        {
            try
            {
                await persistenceCallback(content);
            }
            catch (Exception ex)
            {
                // 持久化失败只记录日志，不影响流式输出
                System.Diagnostics.Debug.WriteLine($"[Warning] ThinkingAccumulator persistence failed: {ex.Message}");
            }
        });
    }
}
