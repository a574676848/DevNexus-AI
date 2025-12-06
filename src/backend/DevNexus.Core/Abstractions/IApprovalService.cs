using DevNexus.Shared.DTOs;

namespace DevNexus.Core.Abstractions;

/// <summary>
/// 人机回环审批服务接口
/// 管理敏感操作的审批流程
/// </summary>
public interface IApprovalService
{
    /// <summary>
    /// 请求审批（阻塞等待用户响应）
    /// </summary>
    /// <param name="request">审批请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>审批响应</returns>
    Task<ApprovalResponse> RequestApprovalAsync(
        ApprovalRequest request,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 提交审批结果（用户批准）
    /// </summary>
    /// <param name="actionId">操作ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task ApproveAsync(Guid actionId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 提交审批结果（用户拒绝）
    /// </summary>
    /// <param name="actionId">操作ID</param>
    /// <param name="reason">拒绝原因</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task RejectAsync(Guid actionId, string? reason = null, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 获取待审批操作
    /// </summary>
    /// <param name="sessionId">会话ID</param>
    /// <returns>待审批操作通知</returns>
    PendingApprovalNotification? GetPendingApproval(Guid sessionId);
    
    /// <summary>
    /// 取消待审批操作
    /// </summary>
    /// <param name="actionId">操作ID</param>
    void CancelPendingApproval(Guid actionId);
}
