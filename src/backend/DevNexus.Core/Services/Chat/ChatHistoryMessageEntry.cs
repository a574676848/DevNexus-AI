namespace DevNexus.Core.Services.Chat;

/// <summary>
/// 构建模型历史时使用的轻量消息项。
/// </summary>
internal sealed record ChatHistoryMessageEntry(string SenderType, string Content);
