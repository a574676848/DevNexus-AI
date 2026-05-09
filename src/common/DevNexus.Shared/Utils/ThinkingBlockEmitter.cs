using DevNexus.Shared.DTOs;
using DevNexus.Shared.Enums;
using System.Threading.Channels;

namespace DevNexus.Shared.Utils;

/// <summary>
/// Thinking 块统一发送器。
/// 为编排、Skill/Plugin 调用等场景提供统一的实时反馈能力。
/// </summary>
public static class ThinkingBlockEmitter
{
    /// <summary>
    /// 创建一条 Thinking Block。
    /// </summary>
    public static BlockDto Create(
        Guid sessionId,
        string content,
        Guid? messageId = null,
        Dictionary<string, object>? metadata = null,
        bool appendNewLine = true)
    {
        var normalizedContent = content ?? string.Empty;
        if (appendNewLine && !normalizedContent.EndsWith("\n", StringComparison.Ordinal))
        {
            normalizedContent += "\n";
        }

        var block = new BlockDto
        {
            SessionId = sessionId,
            BlockType = BlockType.Thinking,
            Content = normalizedContent,
            Metadata = metadata
        };

        if (messageId.HasValue && messageId.Value != Guid.Empty)
        {
            block.MessageId = messageId.Value;
        }

        return block;
    }

    /// <summary>
    /// 将 Thinking Block 写入流通道。通道关闭或取消时静默忽略。
    /// </summary>
    public static async Task EmitAsync(
        ChannelWriter<BlockDto>? blockWriter,
        string sessionId,
        string content,
        CancellationToken cancellationToken = default,
        Guid? messageId = null,
        Dictionary<string, object>? metadata = null,
        bool appendNewLine = true)
    {
        if (blockWriter == null)
        {
            return;
        }

        var parsedSessionId = Guid.TryParse(sessionId, out var sid) ? sid : Guid.Empty;
        await EmitAsync(blockWriter, parsedSessionId, content, cancellationToken, messageId, metadata, appendNewLine);
    }

    /// <summary>
    /// 将 Thinking Block 写入流通道。通道关闭或取消时静默忽略。
    /// </summary>
    public static async Task EmitAsync(
        ChannelWriter<BlockDto>? blockWriter,
        Guid sessionId,
        string content,
        CancellationToken cancellationToken = default,
        Guid? messageId = null,
        Dictionary<string, object>? metadata = null,
        bool appendNewLine = true)
    {
        if (blockWriter == null)
        {
            return;
        }

        try
        {
            var block = Create(sessionId, content, messageId, metadata, appendNewLine);
            await blockWriter.WriteAsync(block, cancellationToken);
        }
        catch (ChannelClosedException)
        {
            // 通道关闭时忽略（用户取消/连接断开）
        }
        catch (OperationCanceledException)
        {
            // 取消时忽略
        }
    }
}
