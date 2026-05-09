using System.Threading;

namespace DevNexus.Core.Services.Swarm.Execution;

/// <summary>
/// Swarm 执行上下文，用于在 CLI/终端执行链路中传递当前工作包信息。
/// </summary>
public static class SwarmExecutionContext
{
    private static readonly AsyncLocal<SwarmExecutionContextState?> CurrentState = new();

    /// <summary>
    /// 当前是否存在激活的工作包执行上下文。
    /// </summary>
    public static bool HasActive => CurrentState.Value != null;

    /// <summary>
    /// 当前工作包 ID。
    /// </summary>
    public static string CurrentPackageId => CurrentState.Value?.PackageId ?? string.Empty;

    /// <summary>
    /// 进入工作包执行上下文作用域。
    /// </summary>
    public static IDisposable BeginScope(string packageId, string packageTitle)
    {
        var previous = CurrentState.Value;
        CurrentState.Value = new SwarmExecutionContextState(packageId);
        return new Scope(previous);
    }

    private sealed record SwarmExecutionContextState(string PackageId);

    private sealed class Scope : IDisposable
    {
        private readonly SwarmExecutionContextState? _previous;
        private bool _disposed;

        public Scope(SwarmExecutionContextState? previous)
        {
            _previous = previous;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            CurrentState.Value = _previous;
            _disposed = true;
        }
    }
}
