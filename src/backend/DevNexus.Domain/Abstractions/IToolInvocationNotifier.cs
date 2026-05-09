using DevNexus.Shared.DTOs;

namespace DevNexus.Domain.Abstractions;

/// <summary>
/// 工具调用状态通知服务接口
/// </summary>
public interface IToolInvocationNotifier
{
    /// <summary>
    /// 通知客户端工具调用状态
    /// </summary>
    Task NotifyToolInvocationAsync(Guid userId, ToolInvocationDto invocation);
}
