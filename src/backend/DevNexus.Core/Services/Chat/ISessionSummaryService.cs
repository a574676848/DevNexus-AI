namespace DevNexus.Core.Services.Chat;

/// <summary>
/// 会话摘要服务。
/// </summary>
public interface ISessionSummaryService
{
    /// <summary>
    /// 获取或生成会话摘要。
    /// </summary>
    Task<string?> GetOrCreateSummaryAsync(
        Guid sessionId,
        Guid providerId,
        string content,
        int targetChars,
        CancellationToken cancellationToken);
}
