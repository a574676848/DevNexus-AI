using DevNexus.Domain.Entities;

namespace DevNexus.Domain.Abstractions;

/// <summary>
/// CLI 执行会话仓储接口。
/// </summary>
public interface ICliExecSessionRepository
{
    /// <summary>
    /// 根据会话键获取执行会话。
    /// </summary>
    Task<CliExecSession?> GetBySessionKeyAsync(string sessionKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// 根据聊天会话获取最近的执行会话。
    /// </summary>
    Task<CliExecSession?> GetLatestByChatSessionIdAsync(Guid chatSessionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 新增或更新执行会话。
    /// </summary>
    Task UpsertAsync(CliExecSession session, CancellationToken cancellationToken = default);
}
