using System.Diagnostics;
using System.Text;
using Microsoft.Extensions.Options;

namespace DevNexus.Infrastructure.Services.CliTerminal;

/// <summary>
/// Docker context 解析器。
/// 负责按“首选 -> 回退 -> 当前默认”的顺序解析当前可用的 docker context。
/// </summary>
public sealed class CliDockerContextResolver
{
    private readonly CliEnvironmentService _cliEnvironmentService;
    private readonly IOptionsMonitor<CliSandboxOptions> _optionsMonitor;

    /// <summary>
    /// 构造函数。
    /// </summary>
    public CliDockerContextResolver(
        CliEnvironmentService cliEnvironmentService,
        IOptionsMonitor<CliSandboxOptions> optionsMonitor)
    {
        _cliEnvironmentService = cliEnvironmentService;
        _optionsMonitor = optionsMonitor;
    }

    /// <summary>
    /// 解析当前应使用的 docker context。
    /// </summary>
    public async Task<string?> ResolveAsync(CancellationToken cancellationToken = default)
    {
        var options = _optionsMonitor.CurrentValue;
        if (!_cliEnvironmentService.IsCommandAvailable(options.ContainerEngineCommand, out var dockerCommandPath))
        {
            throw new InvalidOperationException($"未找到容器运行时命令：{options.ContainerEngineCommand}");
        }

        var candidates = BuildCandidates(options);
        foreach (var candidate in candidates)
        {
            if (await CanUseContextAsync(dockerCommandPath, candidate, cancellationToken))
            {
                return candidate;
            }
        }

        return await CanUseCurrentDefaultAsync(dockerCommandPath, cancellationToken)
            ? null
            : throw new InvalidOperationException("未找到可用的 docker context。");
    }

    private static IEnumerable<string> BuildCandidates(CliSandboxOptions options)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var candidate in new[]
                 {
                     options.PreferredDockerContextName,
                     string.IsNullOrWhiteSpace(options.PreferredDockerContextName) ? options.DockerContextName : null,
                     options.FallbackDockerContextName
                 })
        {
            if (string.IsNullOrWhiteSpace(candidate))
            {
                continue;
            }

            var normalized = candidate.Trim();
            if (seen.Add(normalized))
            {
                yield return normalized;
            }
        }
    }

    private static async Task<bool> CanUseCurrentDefaultAsync(
        string dockerCommandPath,
        CancellationToken cancellationToken)
    {
        var result = await RunDockerProbeAsync(
            dockerCommandPath,
            "version --format \"{{.Server.Version}}\"",
            contextName: null,
            cancellationToken);
        return result.ExitCode == 0 && !string.IsNullOrWhiteSpace(result.StandardOutput);
    }

    private static async Task<bool> CanUseContextAsync(
        string dockerCommandPath,
        string contextName,
        CancellationToken cancellationToken)
    {
        var result = await RunDockerProbeAsync(
            dockerCommandPath,
            "version --format \"{{.Server.Version}}\"",
            contextName,
            cancellationToken);
        return result.ExitCode == 0 && !string.IsNullOrWhiteSpace(result.StandardOutput);
    }

    private static async Task<DockerProbeResult> RunDockerProbeAsync(
        string dockerCommandPath,
        string arguments,
        string? contextName,
        CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = dockerCommandPath,
            Arguments = arguments,
            WorkingDirectory = Directory.GetCurrentDirectory(),
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };
        startInfo.Environment.Remove("DOCKER_HOST");
        if (!string.IsNullOrWhiteSpace(contextName))
        {
            startInfo.Environment["DOCKER_CONTEXT"] = contextName;
        }
        else
        {
            startInfo.Environment.Remove("DOCKER_CONTEXT");
        }

        using var process = new Process { StartInfo = startInfo };
        process.Start();
        var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);
        using var waitCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        waitCts.CancelAfter(TimeSpan.FromSeconds(15));
        await process.WaitForExitAsync(waitCts.Token);

        return new DockerProbeResult(process.ExitCode, await stdoutTask, await stderrTask);
    }

    private sealed record DockerProbeResult(int ExitCode, string StandardOutput, string StandardError);
}
