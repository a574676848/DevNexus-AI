namespace DevNexus.Core.Abstractions;

/// <summary>
/// CLI 审批授权服务。
/// 负责会话级单次授权和同类命令授权。
/// </summary>
public interface ICliApprovalGrantService
{
    /// <summary>
    /// 判断当前命令是否已获授权。
    /// </summary>
    Task<bool> IsApprovedAsync(
        string sessionId,
        string commandFingerprint,
        string commandPattern,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 授权当前命令仅允许一次。
    /// </summary>
    Task GrantOnceAsync(
        Guid? userId,
        Guid? chatSessionId,
        string sessionId,
        string commandFingerprint,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 授权当前命令模式在本会话内持续允许。
    /// </summary>
    Task GrantPatternAsync(
        Guid? userId,
        Guid? chatSessionId,
        string sessionId,
        string commandPattern,
        CancellationToken cancellationToken = default);
}
