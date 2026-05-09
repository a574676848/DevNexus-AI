using System;
using System.Threading;
using System.Threading.Tasks;

namespace DevNexus.Core.Abstractions;

/// <summary>
/// 安全确认服务 - 用于拦截高风险操作并请求用户确认
/// </summary>
public interface IConfirmationService
{
    /// <summary>
    /// 请求用户确认某项操作
    /// 此方法会挂起直到用户响应或超时
    /// </summary>
    /// <param name="sessionId">会话 ID</param>
    /// <param name="operationName">操作名称 (e.g. ExecuteCommand)</param>
    /// <param name="payload">操作参数详情</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>确认结果 (True=批准, False=拒绝)</returns>
    Task<bool> RequestConfirmationAsync(string sessionId, string operationName, string payload, CancellationToken cancellationToken = default);

    /// <summary>
    /// 处理用户对于确认请求的响应
    /// </summary>
    /// <param name="confirmationId">确认请求 ID</param>
    /// <param name="approved">是否批准</param>
    void ResolveConfirmation(string confirmationId, bool approved);
}
