using System.Threading.Channels;
using System.Text;
using DevNexus.Shared.Constants;
using DevNexus.Shared.DTOs;
using DevNexus.Shared.Enums;
using Microsoft.Extensions.Logging;

namespace DevNexus.Core.Services.Chat;

/// <summary>
/// 思维链上下文 - 使用 AsyncLocal 在异步调用链中传递思维链发射器
/// 允许 Plugin 层发送思维链提醒
/// </summary>
public static class ThinkingContext
{
    private static readonly AsyncLocal<ThinkingEmitter?> _emitter = new();

    public static void SetEmitter(ThinkingEmitter emitter)
    {
        _emitter.Value = emitter;
    }

    public static void Clear()
    {
        _emitter.Value = null;
    }

    /// <summary>
    /// 获取当前 Emitter 实例（用于显式捕获，避免 fire-and-forget 场景中 AsyncLocal 丢失）
    /// </summary>
    public static ThinkingEmitter? GetCurrentEmitter() => _emitter.Value;

    public static async Task EmitAsync(
        string message,
        string? source = null,
        ToolInvocationStatus? toolStatus = null)
    {
        if (_emitter.Value != null)
        {
            await _emitter.Value.EmitAsync(message, source, toolStatus);
        }
    }
}

/// <summary>
/// 思维链发射器
/// </summary>
public class ThinkingEmitter
{
    private readonly ChannelWriter<BlockDto> _blockWriter;
    private readonly Guid _sessionId;
    private readonly Guid _messageId;
    private readonly CancellationToken _cancellationToken;
    private readonly StringBuilder _persistenceBuffer = new();
    private readonly ILogger? _logger;

    public ThinkingEmitter(
        ChannelWriter<BlockDto> blockWriter,
        Guid sessionId,
        Guid messageId,
        CancellationToken cancellationToken,
        ILogger? logger = null)
    {
        _blockWriter = blockWriter;
        _sessionId = sessionId;
        _messageId = messageId;
        _cancellationToken = cancellationToken;
        _logger = logger;
    }

    public async Task EmitAsync(
        string message,
        string? source = null,
        ToolInvocationStatus? toolStatus = null)
    {
        var metadata = new Dictionary<string, object>
        {
            { FeedbackBlockMetadataConstants.Source, FeedbackBlockMetadataConstants.NormalizeSource(source) }
        };
        if (toolStatus.HasValue)
        {
            metadata[FeedbackBlockMetadataConstants.ToolStatus] = toolStatus.Value.ToWireValue();
        }

        await _blockWriter.WriteAsync(new BlockDto
        {
            BlockId = Guid.NewGuid(),
            BlockType = BlockType.Thinking,
            Content = message,
            MessageId = _messageId,
            SessionId = _sessionId,
            IsLast = false,
            Metadata = metadata
        }, _cancellationToken);

        // ✅ 同时缓存到持久化缓冲
        _persistenceBuffer.AppendLine(message);

        _logger?.LogDebug(
            "[Thinking.Trace] StreamEmit | Source={Source} SessionId={SessionId} MessageId={MessageId} " +
            "Length={Length} Hash={Hash} Preview={Preview} BufferLength={BufferLength}",
            source ?? "Plugin",
            _sessionId,
            _messageId,
            message.Length,
            ThinkingTraceHelper.ComputeHash(message),
            ThinkingTraceHelper.CreatePreview(message),
            _persistenceBuffer.Length);
    }

    /// <summary>
    /// 获取累积的思维链（用于持久化）
    /// </summary>
    public string GetAccumulatedThinking()
    {
        return _persistenceBuffer.ToString();
    }
}
