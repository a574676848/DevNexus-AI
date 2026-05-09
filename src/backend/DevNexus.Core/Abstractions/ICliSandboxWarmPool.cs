namespace DevNexus.Core.Abstractions;

/// <summary>
/// CLI sandbox 预热池抽象。
/// </summary>
public interface ICliSandboxWarmPool
{
    /// <summary>
    /// 预热指定工作目录对应的 sandbox 模板。
    /// </summary>
    Task WarmAsync(string workingDirectory, CancellationToken cancellationToken = default);
}
