using DevNexus.Shared.DTOs;

namespace DevNexus.Core.Abstractions;

/// <summary>
/// 终端实时通知服务接口 (Phase 7)
/// 负责将 CliSessionManager 的增量输出通过 SignalR 推送到前端
/// </summary>
public interface ITerminalNotifier
{
    /// <summary>
    /// 推送终端增量输出块
    /// </summary>
    /// <param name="userId">用户ID (推流隔离)</param>
    /// <param name="sessionId">会话ID</param>
    /// <param name="messageId">当前处理的消息ID (关联 Block 展示)</param>
    /// <param name="outputDelta">新增的终端输出内容</param>
    /// <param name="isLast">是否执行完毕</param>
    /// <param name="metadata">可选元数据 (如 command, workingDirectory, status)</param>
    Task NotifyTerminalOutputAsync(
        Guid userId, 
        Guid sessionId, 
        Guid messageId, 
        string outputDelta, 
        bool isLast = false,
        Dictionary<string, object>? metadata = null);
}
