using DevNexus.Shared.Enums;

namespace DevNexus.Infrastructure.Services.CliTerminal;

/// <summary>
/// CLI sandbox 配置选项。
/// </summary>
public sealed class CliSandboxOptions
{
    /// <summary>
    /// 当前启用的 sandbox 模式。
    /// </summary>
    public CliSandboxMode Mode { get; set; } = CliSandboxMode.LocalRestricted;

    /// <summary>
    /// 容器运行时命令。
    /// 默认使用 docker。
    /// </summary>
    public string ContainerEngineCommand { get; set; } = "docker";

    /// <summary>
    /// 兼容旧配置的显式 docker context 名称。
    /// 如已设置 PreferredDockerContextName，则优先使用新字段。
    /// </summary>
    public string? DockerContextName { get; set; }

    /// <summary>
    /// 首选 docker context 名称。
    /// 默认优先使用本机 default 上下文，确保工作目录 bind mount 与当前宿主一致。
    /// </summary>
    public string? PreferredDockerContextName { get; set; } = "default";

    /// <summary>
    /// 回退 docker context 名称。
    /// 当前主要用于本机 docker 不可用时退到远程 docker。
    /// </summary>
    public string? FallbackDockerContextName { get; set; }

    /// <summary>
    /// 容器镜像。
    /// </summary>
    public string ContainerImage { get; set; } = "mcr.microsoft.com/dotnet/sdk:10.0";

    /// <summary>
    /// 容器内默认 shell。
    /// </summary>
    public string ContainerShell { get; set; } = "/bin/bash";

    /// <summary>
    /// 容器内工作目录挂载点。
    /// </summary>
    public string ContainerWorkingDirectory { get; set; } = "/workspace";

    /// <summary>
    /// 是否禁用容器网络。
    /// </summary>
    public bool DisableNetwork { get; set; } = true;

    /// <summary>
    /// 容器内存限制（MB）。
    /// 小于等于 0 表示不显式限制。
    /// </summary>
    public int MemoryLimitMb { get; set; } = 512;

    /// <summary>
    /// 容器 CPU 限制。
    /// 小于等于 0 表示不显式限制。
    /// </summary>
    public decimal CpuLimit { get; set; } = 1.0m;
}
