using DevNexus.Core.Models.Cli;
using DevNexus.Shared.DTOs;

namespace DevNexus.Core.Abstractions;

/// <summary>
/// CLI 进程态服务。
/// 负责围绕已创建会话执行轮询、日志读取、输入、终止和回滚。
/// </summary>
public interface ICliProcessService
{
    Task<CliExecSessionDto?> GetSessionAsync(
        Guid userId,
        Guid sessionId,
        CancellationToken cancellationToken = default);

    Task<CliExecLogChunkDto?> GetLogChunkAsync(
        Guid userId,
        Guid sessionId,
        int startIndex = 0,
        CancellationToken cancellationToken = default);

    Task<CliExecSessionDto?> WaitForExitAsync(
        Guid userId,
        Guid sessionId,
        TimeSpan timeout,
        CancellationToken cancellationToken = default);

    Task<CliExecSessionDto> WriteInputAsync(
        Guid userId,
        Guid sessionId,
        string input,
        CancellationToken cancellationToken = default);

    Task<CliExecTerminateResultDto> TerminateAsync(
        Guid userId,
        Guid sessionId,
        CancellationToken cancellationToken = default);

    Task<CliExecRollbackResultDto> RollbackAsync(
        Guid userId,
        Guid sessionId,
        CancellationToken cancellationToken = default);

    Task<CliSessionRuntimeSnapshot?> GetRuntimeSnapshotAsync(
        Guid userId,
        Guid sessionId,
        CancellationToken cancellationToken = default);

    Task<CliExecCheckpointDto?> GetLatestCheckpointAsync(
        Guid userId,
        Guid sessionId,
        CancellationToken cancellationToken = default);
}
