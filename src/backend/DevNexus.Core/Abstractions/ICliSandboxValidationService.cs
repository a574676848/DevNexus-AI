using DevNexus.Shared.DTOs;

namespace DevNexus.Core.Abstractions;

/// <summary>
/// CLI sandbox 验证服务。
/// </summary>
public interface ICliSandboxValidationService
{
    /// <summary>
    /// 验证容器 sandbox 能否通过当前默认 docker 上下文正常启动。
    /// </summary>
    Task<CliSandboxValidationResultDto> ValidateContainerSandboxAsync(CancellationToken cancellationToken = default);
}
