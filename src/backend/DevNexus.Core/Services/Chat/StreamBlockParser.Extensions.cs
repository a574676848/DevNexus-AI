using DevNexus.Shared.Constants;
using DevNexus.Shared.DTOs;
using DevNexus.Shared.Enums;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;

namespace DevNexus.Core.Services.Chat;

public partial class StreamBlockParser
{
    /// <summary>
    /// 发送思考过程 Block
    /// </summary>
    /// <param name="thoughtContent">思考内容</param>
    /// <param name="blockWriter">通道写入器</param>
    /// <param name="sessionId">会话 ID</param>
    /// <param name="messageId">消息 ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task EmitThoughtBlockAsync(
        string thoughtContent,
        ChannelWriter<BlockDto> blockWriter,
        Guid sessionId,
        Guid messageId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(thoughtContent)) return;

        // ★ 累积思维链内容到内部缓冲区（用于后续持久化）
        _thinkingBuffer.Append(thoughtContent);
        _thinkingBlockCount++;
        
        // ★ 刷新到流式输出
        var block = new BlockDto
        {
            BlockType = BlockType.Thinking,
            Content = thoughtContent,
            MessageId = messageId,
            SessionId = sessionId,
            IsLast = false,
            Metadata = new Dictionary<string, object>
            {
                { FeedbackBlockMetadataConstants.Collapsed, true } // 默认折叠思考过程
            }
        };

        await blockWriter.WriteAsync(block, cancellationToken);

        _logger?.LogDebug(
            "[Thinking.Trace] StreamEmit | Source={Source} SessionId={SessionId} MessageId={MessageId} " +
            "Length={Length} Hash={Hash} Preview={Preview} BufferLength={BufferLength} PendingBlocks={PendingBlocks}",
            "ReasoningDelta",
            sessionId,
            messageId,
            thoughtContent.Length,
            ThinkingTraceHelper.ComputeHash(thoughtContent),
            ThinkingTraceHelper.CreatePreview(thoughtContent),
            _thinkingBuffer.Length,
            _thinkingBlockCount);
        
        // ★ 达到阈值时触发周期性持久化（异步、不阻塞流）
        if (_thinkingBlockCount >= PERSISTENCE_THRESHOLD && _persistenceCallback != null)
        {
            var partialThinking = GetAndClearThinkingBuffer();
            if (string.IsNullOrEmpty(partialThinking))
            {
                return;
            }
            
            // 后台异步持久化（fire-and-forget，不等待完成）
            _ = Task.Run(async () =>
            {
                try
                {
                    _logger?.LogDebug(
                        "[Thinking.Trace] PersistDispatch | Source={Source} SessionId={SessionId} MessageId={MessageId} " +
                        "Length={Length} Hash={Hash}",
                        "ReasoningDelta",
                        sessionId,
                        messageId,
                        partialThinking.Length,
                        ThinkingTraceHelper.ComputeHash(partialThinking));

                    await _persistenceCallback(partialThinking, sessionId, messageId);
                }
                catch (Exception ex)
                {
                    // 持久化失败不影响流式输出（仅记录日志）
                    System.Diagnostics.Debug.WriteLine($"[Warning] Failed to persist thinking: {ex.Message}");
                }
            });
        }
    }
    
    /// <summary>
    /// 获取已累积的思维链并清空缓冲区（用于周期性持久化）
    /// </summary>
    public string GetAndClearThinkingBuffer()
    {
        var content = _thinkingBuffer.ToString();
        _thinkingBuffer.Clear();
        _thinkingBlockCount = 0;
        return content;
    }
    
    /// <summary>
    /// 获取累积的思维链内容（用于持久化）
    /// </summary>
    public string GetAccumulatedThinking()
    {
        return _thinkingBuffer.ToString();
    }
    
    /// <summary>
    /// 清空思维链缓冲区
    /// </summary>
    public void ClearThinking()
    {
        _thinkingBuffer.Clear();
        _thinkingBlockCount = 0;
    }
}
