using DevNexus.Core.Models.Cli;

namespace DevNexus.Core.Abstractions;

/// <summary>
/// CLI 进程注册表抽象。
/// 负责会话创建、输入写入、输出读取、终止和超时回收。
/// </summary>
public interface ICliProcessRegistry : IDisposable
{
    event Action<string, string>? OnOutputReceived;

    Task<string> CreateSessionAsync(string sessionId, string workingDirectory, CancellationToken ct);

    Task WriteAsync(string sessionId, string input, CancellationToken ct);

    Task<(string Output, int ExitCode)> ExecuteAndWaitAsync(
        string sessionId,
        string command,
        TimeSpan timeout,
        CancellationToken ct);

    CliSessionTerminationReason GetTerminationReason(string sessionId);

    string GetStrippedOutput(string sessionId, int startIndex = 0);

    string GetRawOutput(string sessionId, int startIndex = 0);

    string TruncateOutput(string output, int headLimit = 1500, int tailLimit = 3500);

    /// <summary>
    /// 获取最近输出尾部。
    /// </summary>
    string GetOutputTail(string sessionId, int maxChars = 4000);

    void TerminateSession(string sessionId);

    void CleanupBuffers(string sessionId);

    CliRuntimeCleanupResult CleanupExpiredSessions(
        TimeSpan idleTimeout,
        TimeSpan waitingForInputTimeout,
        TimeSpan maxRuntime);

    void MarkSessionTerminated(
        string sessionId,
        CliSessionExecutionState state,
        CliSessionTerminationReason terminationReason);

    CliSessionRuntimeSnapshot? GetRuntimeSnapshot(string sessionId);

    /// <summary>
    /// 等待会话进入终态或超时。
    /// </summary>
    Task<CliSessionRuntimeSnapshot?> WaitForExitAsync(
        string sessionId,
        TimeSpan timeout,
        CancellationToken cancellationToken);
}
