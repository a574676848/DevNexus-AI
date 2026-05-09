using DevNexus.Shared.DTOs;

namespace DevNexus.Core.Abstractions;

/// <summary>
/// 终端输出缓冲服务接口
/// </summary>
public interface ITerminalOutputBuffer
{
    /// <summary>
    /// 追加输出到内存缓冲
    /// </summary>
    /// <param name="streamId">终端流 ID</param>
    /// <param name="outputDelta">输出增量</param>
    /// <param name="metadata">元数据（包含 command, workingDirectory, attemptNumber 等）</param>
    Task AppendAsync(Guid streamId, string outputDelta, Dictionary<string, object>? metadata = null);

    /// <summary>
    /// 刷新指定流到数据库（流结束时调用）
    /// </summary>
    /// <param name="streamId">终端流 ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>是否成功刷新</returns>
    Task<bool> FlushAsync(Guid streamId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 定时刷新所有缓冲（后台任务）
    /// </summary>
    Task FlushAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取缓冲统计信息
    /// </summary>
    BufferStatistics GetStatistics();

    /// <summary>
    /// 清理指定流的缓冲（流结束后调用）
    /// </summary>
    void Remove(Guid streamId);

    /// <summary>
    /// 读取指定终端流的完整输出。
    /// </summary>
    Task<TerminalOutputContentDto?> ReadOutputAsync(Guid streamId, CancellationToken cancellationToken = default);
}

/// <summary>
/// 缓冲统计信息
/// </summary>
public class BufferStatistics
{
    /// <summary>
    /// 当前缓冲的流数量
    /// </summary>
    public int ActiveStreams { get; set; }

    /// <summary>
    /// 总缓冲大小（字节）
    /// </summary>
    public long TotalBufferSize { get; set; }

    /// <summary>
    /// 总刷新次数
    /// </summary>
    public long TotalFlushCount { get; set; }

    /// <summary>
    /// 失败次数
    /// </summary>
    public long FailedFlushCount { get; set; }
}
