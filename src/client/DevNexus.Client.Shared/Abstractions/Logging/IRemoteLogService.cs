namespace DevNexus.Client.Shared.Abstractions;

/// <summary>
/// 远程日志服务接口
/// 用于将客户端日志（尤其是异常）批量上报到服务端
/// </summary>
public interface IRemoteLogService : IDisposable
{
    /// <summary>
    /// 记录错误日志并上报到服务端
    /// </summary>
    /// <param name="exception">异常对象</param>
    /// <param name="source">异常来源（如 ApiService, SignalRService, ErrorBoundary）</param>
    /// <param name="additionalData">附加数据（可选）</param>
    Task LogErrorAsync(Exception exception, string source,
        Dictionary<string, object?>? additionalData = null);

    /// <summary>
    /// 记录警告日志并上报到服务端
    /// </summary>
    /// <param name="message">警告消息</param>
    /// <param name="source">来源</param>
    /// <param name="additionalData">附加数据（可选）</param>
    Task LogWarningAsync(string message, string source,
        Dictionary<string, object?>? additionalData = null);

    /// <summary>
    /// 立即刷新队列（应用退出时调用）
    /// </summary>
    Task FlushAsync();
}

