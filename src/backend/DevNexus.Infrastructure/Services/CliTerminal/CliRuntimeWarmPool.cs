using DevNexus.Core.Abstractions;

namespace DevNexus.Infrastructure.Services.CliTerminal;

/// <summary>
/// CLI 运行时热池实现。
/// 负责预热可直接消费的 shell 实例。
/// </summary>
public sealed class CliRuntimeWarmPool : ICliSandboxWarmPool
{
    private readonly ProcessCliRuntimeHost _runtimeHost;

    /// <summary>
    /// 构造函数。
    /// </summary>
    public CliRuntimeWarmPool(ProcessCliRuntimeHost runtimeHost)
    {
        _runtimeHost = runtimeHost;
    }

    /// <inheritdoc />
    public Task WarmAsync(string workingDirectory, CancellationToken cancellationToken = default)
    {
        return _runtimeHost.WarmShellAsync(workingDirectory, cancellationToken);
    }
}
