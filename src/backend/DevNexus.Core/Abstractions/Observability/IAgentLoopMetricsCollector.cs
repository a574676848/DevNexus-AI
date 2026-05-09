namespace DevNexus.Core.Abstractions.Observability;

/// <summary>
/// Agent Loop 指标收集器接口 - 定义在 Core 中供上层依赖
/// 实现在 Infrastructure 中，符合洋葱架构
/// </summary>
public interface IAgentLoopMetricsCollector
{
    /// <summary>
    /// 记录自动修复尝试
    /// </summary>
    /// <param name="success">是否成功</param>
    /// <param name="durationMs">耗时（毫秒）</param>
    Task RecordRepairAttempt(bool success, long durationMs);

    /// <summary>
    /// 记录到达最大尝试次数
    /// </summary>
    /// <param name="totalAttempts">总尝试次数</param>
    Task RecordMaxAttemptsReached(int totalAttempts);

    /// <summary>
    /// 记录工具执行
    /// </summary>
    /// <param name="toolName">工具名称</param>
    /// <param name="success">是否成功</param>
    /// <param name="durationMs">耗时（毫秒）</param>
    Task RecordToolExecution(string toolName, bool success, long durationMs);

    /// <summary>
    /// 记录终端输出
    /// </summary>
    /// <param name="outputBytes">输出字节数</param>
    /// <param name="chunkCount">块数</param>
    /// <param name="persistLatencyMs">持久化延迟（毫秒）</param>
    Task RecordTerminalOutput(long outputBytes, int chunkCount, long persistLatencyMs);

    /// <summary>
    /// 记录会话恢复
    /// </summary>
    /// <param name="success">是否成功</param>
    /// <param name="recoveredMessageCount">恢复的消息数量</param>
    Task RecordSessionRecovery(bool success, int recoveredMessageCount);

    /// <summary>
    /// 获取所有指标的当前状态
    /// </summary>
    /// <returns>指标快照字典</returns>
    Dictionary<string, object> GetMetricsSnapshot();

    /// <summary>
    /// 重置所有指标
    /// </summary>
    void ResetMetrics();
}
