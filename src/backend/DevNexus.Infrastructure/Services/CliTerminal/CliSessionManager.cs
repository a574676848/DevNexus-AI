using DevNexus.Core.Abstractions;
using DevNexus.Core.Models.Cli;
using DevNexus.Core.Services.Terminal;

namespace DevNexus.Infrastructure.Services.CliTerminal;

/// <summary>
/// CLI 会话管理门面。
/// 对外保持既有调用接口，内部委托给具体运行时宿主。
/// </summary>
public class CliSessionManager : IDisposable
{
    private readonly ICliProcessRegistry _processRegistry;

    /// <summary>
    /// 当有新输出产生时触发。参数：sessionId, deltaText
    /// </summary>
    public event Action<string, string>? OnOutputReceived
    {
        add => _processRegistry.OnOutputReceived += value;
        remove => _processRegistry.OnOutputReceived -= value;
    }

    public CliSessionManager(ICliProcessRegistry processRegistry)
    {
        _processRegistry = processRegistry;
    }

    public Task<string> CreateSessionAsync(
        string sessionId,
        string workingDirectory,
        CancellationToken ct)
    {
        return _processRegistry.CreateSessionAsync(sessionId, workingDirectory, ct);
    }

    public async Task WriteAsync(string sessionId, string input, CancellationToken ct)
    {
        await _processRegistry.WriteAsync(sessionId, input, ct);
    }

    /// <summary>
    /// 带哨兵机制的执行命令，适用于后端 LLM 工具调用
    /// </summary>
    public async Task<CliCommandExecutionResult> ExecuteAndWaitAsync(
        string sessionId,
        string command,
        TimeSpan timeout,
        CancellationToken ct)
    {
        return await _processRegistry.ExecuteAndWaitAsync(sessionId, command, timeout, ct);
    }

    public CliSessionTerminationReason GetTerminationReason(string sessionId)
    {
        return _processRegistry.GetTerminationReason(sessionId);
    }

    public string GetStrippedOutput(string sessionId, int startIndex = 0)
    {
        return _processRegistry.GetStrippedOutput(sessionId, startIndex);
    }

    public string GetRawOutput(string sessionId, int startIndex = 0)
    {
        return _processRegistry.GetRawOutput(sessionId, startIndex);
    }

    /// <summary>
    /// 截断过长输出发给 LLM，优先保留 Head（命令回显和初期错误）和 Tail（总结性报错）
    /// </summary>
    public string TruncateOutput(string output, int headLimit = 1500, int tailLimit = 3500)
    {
        return TerminalOutputPreviewBuilder.Build(output, headLimit, tailLimit);
    }

    public void TerminateSession(string sessionId)
    {
        _processRegistry.TerminateSession(sessionId);
    }

    public void CleanupBuffers(string sessionId)
    {
        _processRegistry.CleanupBuffers(sessionId);
    }

    public CliRuntimeCleanupResult CleanupExpiredSessions(
        TimeSpan idleTimeout,
        TimeSpan waitingForInputTimeout,
        TimeSpan maxRuntime)
    {
        return _processRegistry.CleanupExpiredSessions(idleTimeout, waitingForInputTimeout, maxRuntime);
    }

    public void MarkSessionTerminated(
        string sessionId,
        CliSessionExecutionState state,
        CliSessionTerminationReason terminationReason)
    {
        _processRegistry.MarkSessionTerminated(sessionId, state, terminationReason);
    }

    public CliSessionRuntimeSnapshot? GetRuntimeSnapshot(string sessionId)
    {
        return _processRegistry.GetRuntimeSnapshot(sessionId);
    }

    public void Dispose()
    {
        // 由 DI 容器统一释放底层运行时宿主，避免重复 Dispose。
    }
}
