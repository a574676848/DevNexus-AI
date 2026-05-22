using Microsoft.Extensions.Logging;

namespace DevNexus.Infrastructure.Services.CliTerminal;

public sealed partial class ProcessCliRuntimeHost
{
    /// <summary>
    /// 预热指定工作目录的可消费 shell。
    /// </summary>
    public async Task WarmShellAsync(string workingDirectory, CancellationToken cancellationToken)
    {
        CleanupExpiredWarmShells();

        var normalizedWorkingDirectory = Path.GetFullPath(workingDirectory);
        if (_warmShells.TryGetValue(normalizedWorkingDirectory, out var existing)
            && !existing.Process.HasExited)
        {
            return;
        }

        var warmSessionId = $"warm:{Guid.NewGuid():N}";
        var lease = await _sandboxSessionProvider.AcquireAsync(warmSessionId, normalizedWorkingDirectory, cancellationToken);
        var process = StartPersistentShell(lease, warmSessionId);

        if (process.HasExited)
        {
            _sandboxSessionProvider.Release(warmSessionId);
            process.Dispose();
            return;
        }

        var entry = new WarmShellEntry
        {
            WorkingDirectory = normalizedWorkingDirectory,
            Lease = lease,
            Process = process,
            WarmedAt = DateTime.UtcNow
        };

        if (_warmShells.TryGetValue(normalizedWorkingDirectory, out var previous))
        {
            CleanupWarmShell(previous);
        }

        _warmShells[normalizedWorkingDirectory] = entry;
        _logger.LogDebug(
            "[CliRuntimeWarmPool] 已预热 shell | WorkingDirectory={WorkingDirectory} WarmSession={WarmSession}",
            normalizedWorkingDirectory,
            warmSessionId);
    }

    private WarmShellEntry? TryTakeWarmShell(string workingDirectory)
    {
        if (!_warmShells.TryRemove(workingDirectory, out var entry))
        {
            return null;
        }

        if (entry.Process.HasExited)
        {
            CleanupWarmShell(entry);
            return null;
        }

        return entry;
    }

    private void CleanupExpiredWarmShells()
    {
        var now = DateTime.UtcNow;
        foreach (var entry in _warmShells.ToArray())
        {
            if (entry.Value.Process.HasExited || now - entry.Value.WarmedAt > WarmShellMaxAge)
            {
                if (_warmShells.TryRemove(entry.Key, out var removed))
                {
                    CleanupWarmShell(removed);
                }
            }
        }
    }

    private void ReleaseWarmShell(string workingDirectory)
    {
        if (_warmShells.TryRemove(workingDirectory, out var entry))
        {
            CleanupWarmShell(entry);
        }
    }

    private void CleanupWarmShell(WarmShellEntry entry)
    {
        try
        {
            _sandboxSessionProvider.Release(entry.Lease.SessionId);
            if (!entry.Process.HasExited)
            {
                entry.Process.Kill(entireProcessTree: true);
                entry.Process.WaitForExit();
            }
        }
        catch
        {
        }
        finally
        {
            entry.Process.Dispose();
        }
    }
}
