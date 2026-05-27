using DevNexus.Core.Abstractions;
using DevNexus.Domain.Abstractions;
using DevNexus.Domain.Entities;
using DevNexus.Shared.DTOs;
using DevNexus.Shared.Enums;
using Microsoft.Extensions.Logging;

namespace DevNexus.Infrastructure.Services.CliTerminal;

/// <summary>
/// CLI 执行快照服务实现。
/// </summary>
public sealed class CliExecCheckpointService : ICliExecCheckpointService
{
    private static readonly string[] HighRiskCommandPrefixes =
    [
        "rm ",
        "del ",
        "erase ",
        "mv ",
        "move-item ",
        "rename-item ",
        "git clean",
        "git reset --hard",
        "git checkout --",
        "set-content ",
        "out-file "
    ];

    private static readonly string CheckpointRoot = Path.Combine(Path.GetTempPath(), "DevNexus-AI", "cli-checkpoints");

    private readonly ILogger<CliExecCheckpointService> _logger;
    private readonly ICliExecCheckpointRepository _checkpointRepository;
    private readonly ICliProcessRegistry _cliProcessRegistry;

    /// <summary>
    /// 构造函数。
    /// </summary>
    public CliExecCheckpointService(
        ILogger<CliExecCheckpointService> logger,
        ICliExecCheckpointRepository checkpointRepository,
        ICliProcessRegistry cliProcessRegistry)
    {
        _logger = logger;
        _checkpointRepository = checkpointRepository;
        _cliProcessRegistry = cliProcessRegistry;
    }

    /// <inheritdoc />
    public async Task CreateCheckpointIfNeededAsync(
        Guid userId,
        Guid? chatSessionId,
        string sessionKey,
        string command,
        string workingDirectory,
        CancellationToken cancellationToken = default)
    {
        if (!ShouldCheckpoint(command) || string.IsNullOrWhiteSpace(workingDirectory))
        {
            return;
        }

        var normalizedWorkingDirectory = NormalizePath(workingDirectory);
        if (string.IsNullOrWhiteSpace(normalizedWorkingDirectory) || !Directory.Exists(normalizedWorkingDirectory))
        {
            return;
        }

        var checkpointId = Guid.NewGuid();
        var userCheckpointRoot = GetUserCheckpointRoot(userId);
        var snapshotDirectory = Path.Combine(userCheckpointRoot, checkpointId.ToString("N"));
        Directory.CreateDirectory(userCheckpointRoot);

        try
        {
            CopyDirectory(normalizedWorkingDirectory, snapshotDirectory);

            await _checkpointRepository.AddAsync(new CliExecCheckpoint
            {
                Id = checkpointId,
                UserId = userId,
                ChatSessionId = chatSessionId,
                SessionKey = sessionKey,
                Command = command,
                WorkingDirectory = normalizedWorkingDirectory,
                SnapshotDirectory = snapshotDirectory,
                Status = CliExecCheckpointStatus.Created
            }, cancellationToken);

            var staleCheckpoints = await _checkpointRepository.GetActiveBySessionKeyAsync(sessionKey, cancellationToken);
            var replacedCheckpoints = staleCheckpoints
                .Where(checkpoint => checkpoint.Id != checkpointId)
                .ToList();
            if (replacedCheckpoints.Count > 0)
            {
                foreach (var checkpoint in replacedCheckpoints)
                {
                    checkpoint.Status = CliExecCheckpointStatus.Invalidated;
                    checkpoint.UpdatedAt = DateTime.UtcNow;
                }

                await _checkpointRepository.UpdateRangeAsync(replacedCheckpoints, cancellationToken);

                foreach (var checkpoint in replacedCheckpoints)
                {
                    TryDeleteDirectory(checkpoint.SnapshotDirectory);
                }
            }
        }
        catch
        {
            TryDeleteDirectory(snapshotDirectory);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<CliExecRollbackResultDto> RollbackLatestAsync(
        Guid userId,
        Guid sessionId,
        string sessionKey,
        CancellationToken cancellationToken = default)
    {
        var runtimeSnapshot = _cliProcessRegistry.GetRuntimeSnapshot(sessionKey);
        if (runtimeSnapshot != null && IsRuntimeStateActive(runtimeSnapshot.State))
        {
            return new CliExecRollbackResultDto
            {
                SessionId = sessionId,
                RolledBack = false,
                Message = "终端仍在执行，不能在运行中回滚。",
                WorkingDirectory = runtimeSnapshot.WorkingDirectory
            };
        }

        var checkpoint = await _checkpointRepository.GetLatestActiveBySessionKeyAsync(sessionKey, cancellationToken);
        if (checkpoint == null)
        {
            return new CliExecRollbackResultDto
            {
                SessionId = sessionId,
                RolledBack = false,
                Message = "当前会话没有可回滚的快照。",
                WorkingDirectory = null
            };
        }

        if (!Directory.Exists(checkpoint.SnapshotDirectory))
        {
            await InvalidateCheckpointAsync(checkpoint, cancellationToken);
            return new CliExecRollbackResultDto
            {
                SessionId = sessionId,
                RolledBack = false,
                Message = "快照文件已丢失，无法执行回滚。",
                WorkingDirectory = checkpoint.WorkingDirectory
            };
        }

        var backupDirectory = Path.Combine(GetUserCheckpointRoot(userId), $"restore-backup-{Guid.NewGuid():N}");

        try
        {
            if (Directory.Exists(checkpoint.WorkingDirectory))
            {
                CopyDirectory(checkpoint.WorkingDirectory, backupDirectory);
            }

            RestoreDirectory(checkpoint.SnapshotDirectory, checkpoint.WorkingDirectory);
            checkpoint.Status = CliExecCheckpointStatus.RolledBack;
            checkpoint.RolledBackAt = DateTime.UtcNow;
            checkpoint.UpdatedAt = DateTime.UtcNow;
            await _checkpointRepository.UpdateAsync(checkpoint, cancellationToken);

            TryDeleteDirectory(backupDirectory);
            TryDeleteDirectory(checkpoint.SnapshotDirectory);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "CLI 快照回滚失败，开始尝试恢复原目录 | SessionKey={SessionKey} WorkingDirectory={WorkingDirectory}",
                sessionKey,
                checkpoint.WorkingDirectory);

            try
            {
                if (Directory.Exists(backupDirectory))
                {
                    RestoreDirectory(backupDirectory, checkpoint.WorkingDirectory);
                }
            }
            catch (Exception restoreEx)
            {
                _logger.LogError(
                    restoreEx,
                    "CLI 快照回滚失败且原目录恢复失败 | SessionKey={SessionKey} WorkingDirectory={WorkingDirectory}",
                    sessionKey,
                    checkpoint.WorkingDirectory);
            }
            finally
            {
                TryDeleteDirectory(backupDirectory);
            }

            return new CliExecRollbackResultDto
            {
                SessionId = sessionId,
                RolledBack = false,
                Message = "回滚执行失败，已尝试恢复回滚前目录。",
                WorkingDirectory = checkpoint.WorkingDirectory
            };
        }

        return new CliExecRollbackResultDto
        {
            SessionId = sessionId,
            RolledBack = true,
            Message = "已回滚到最近一次命令执行前的快照。",
            WorkingDirectory = checkpoint.WorkingDirectory
        };
    }

    private static bool ShouldCheckpoint(string command)
    {
        if (string.IsNullOrWhiteSpace(command))
        {
            return false;
        }

        var normalized = command.Trim().ToLowerInvariant();
        return HighRiskCommandPrefixes.Any(prefix => normalized.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
    }

    private static string GetUserCheckpointRoot(Guid userId)
    {
        return Path.Combine(CheckpointRoot, userId.ToString("N"));
    }

    private static string NormalizePath(string path)
    {
        try
        {
            return Path.GetFullPath(path);
        }
        catch
        {
            return string.Empty;
        }
    }

    private static bool IsRuntimeStateActive(Core.Models.Cli.CliSessionExecutionState state)
    {
        return state is Core.Models.Cli.CliSessionExecutionState.Created
            or Core.Models.Cli.CliSessionExecutionState.Running
            or Core.Models.Cli.CliSessionExecutionState.WaitingForInput;
    }

    private static void CopyDirectory(string sourceDirectory, string destinationDirectory)
    {
        var source = new DirectoryInfo(sourceDirectory);
        Directory.CreateDirectory(destinationDirectory);

        foreach (var directory in source.GetDirectories("*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(sourceDirectory, directory.FullName);
            Directory.CreateDirectory(Path.Combine(destinationDirectory, relative));
        }

        foreach (var file in source.GetFiles("*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(sourceDirectory, file.FullName);
            var destinationPath = Path.Combine(destinationDirectory, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
            file.CopyTo(destinationPath, overwrite: true);
        }
    }

    private static void RestoreDirectory(string snapshotDirectory, string targetDirectory)
    {
        ClearDirectory(targetDirectory);
        CopyDirectory(snapshotDirectory, targetDirectory);
    }

    private static void ClearDirectory(string targetDirectory)
    {
        if (!Directory.Exists(targetDirectory))
        {
            Directory.CreateDirectory(targetDirectory);
            return;
        }

        var directoryInfo = new DirectoryInfo(targetDirectory);
        foreach (var entry in directoryInfo.EnumerateFileSystemInfos())
        {
            switch (entry)
            {
                case DirectoryInfo subDirectory:
                    subDirectory.Delete(recursive: true);
                    break;
                case FileInfo file:
                    file.Delete();
                    break;
            }
        }
    }

    private async Task InvalidateCheckpointAsync(
        CliExecCheckpoint checkpoint,
        CancellationToken cancellationToken)
    {
        checkpoint.Status = CliExecCheckpointStatus.Invalidated;
        checkpoint.UpdatedAt = DateTime.UtcNow;
        await _checkpointRepository.UpdateAsync(checkpoint, cancellationToken);
        TryDeleteDirectory(checkpoint.SnapshotDirectory);
    }

    private static void TryDeleteDirectory(string? directory)
    {
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
        {
            return;
        }

        try
        {
            Directory.Delete(directory, recursive: true);
        }
        catch
        {
            // 清理失败不影响主流程，下次创建或回滚时再做覆盖处理。
        }
    }
}
