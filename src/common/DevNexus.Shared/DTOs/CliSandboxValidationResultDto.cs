using DevNexus.Shared.Enums;

namespace DevNexus.Shared.DTOs;

/// <summary>
/// CLI sandbox 验证结果 DTO。
/// </summary>
public sealed class CliSandboxValidationResultDto
{
    /// <summary>
    /// 是否验证成功。
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// 当前配置的 sandbox 模式。
    /// </summary>
    public CliSandboxMode ConfiguredMode { get; set; } = CliSandboxMode.LocalRestricted;

    /// <summary>
    /// 实际执行验证的 provider。
    /// </summary>
    public string Provider { get; set; } = string.Empty;

    /// <summary>
    /// 当前 docker context。
    /// </summary>
    public string DockerContext { get; set; } = string.Empty;

    /// <summary>
    /// docker CLI 路径。
    /// </summary>
    public string DockerCommandPath { get; set; } = string.Empty;

    /// <summary>
    /// Docker 服务端版本摘要。
    /// </summary>
    public string DockerServer { get; set; } = string.Empty;

    /// <summary>
    /// 验证使用的容器镜像。
    /// </summary>
    public string ContainerImage { get; set; } = string.Empty;

    /// <summary>
    /// 验证使用的容器 shell。
    /// </summary>
    public string ContainerShell { get; set; } = string.Empty;

    /// <summary>
    /// 宿主机工作目录。
    /// </summary>
    public string HostWorkingDirectory { get; set; } = string.Empty;

    /// <summary>
    /// 容器工作目录。
    /// </summary>
    public string ContainerWorkingDirectory { get; set; } = string.Empty;

    /// <summary>
    /// 是否成功进入容器 shell。
    /// </summary>
    public bool ShellStarted { get; set; }

    /// <summary>
    /// 容器内是否能看到宿主机验证文件。
    /// </summary>
    public bool ProbeFileVisible { get; set; }

    /// <summary>
    /// 探针输出。
    /// </summary>
    public string ProbeOutput { get; set; } = string.Empty;

    /// <summary>
    /// 标准错误输出。
    /// </summary>
    public string ErrorOutput { get; set; } = string.Empty;

    /// <summary>
    /// 失败原因。
    /// </summary>
    public string? FailureReason { get; set; }

    /// <summary>
    /// 总耗时毫秒。
    /// </summary>
    public long ElapsedMilliseconds { get; set; }
}
