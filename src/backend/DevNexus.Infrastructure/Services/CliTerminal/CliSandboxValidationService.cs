using System.Diagnostics;
using System.Text;
using DevNexus.Core.Abstractions;
using DevNexus.Shared.DTOs;
using DevNexus.Shared.Enums;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DevNexus.Infrastructure.Services.CliTerminal;

/// <summary>
/// CLI sandbox 验证服务实现。
/// 直接基于容器 sandbox provider 验证当前 docker 默认上下文是否可用。
/// </summary>
public sealed class CliSandboxValidationService : ICliSandboxValidationService
{
    private const string ProbeFileName = "devnexus-sandbox-probe.txt";
    private const string ProbeSuccessMarker = "DEVNEXUS_SANDBOX_OK";
    private const string ProbeFileMarker = "DEVNEXUS_PROBE_FILE_OK";

    private readonly CliEnvironmentService _cliEnvironmentService;
    private readonly CliDockerContextResolver _dockerContextResolver;
    private readonly ContainerSandboxSessionProvider _containerSandboxSessionProvider;
    private readonly CliSandboxOptions _options;
    private readonly ILogger<CliSandboxValidationService> _logger;

    /// <summary>
    /// 构造函数。
    /// </summary>
    public CliSandboxValidationService(
        CliEnvironmentService cliEnvironmentService,
        CliDockerContextResolver dockerContextResolver,
        ContainerSandboxSessionProvider containerSandboxSessionProvider,
        IOptions<CliSandboxOptions> options,
        ILogger<CliSandboxValidationService> logger)
    {
        _cliEnvironmentService = cliEnvironmentService;
        _dockerContextResolver = dockerContextResolver;
        _containerSandboxSessionProvider = containerSandboxSessionProvider;
        _options = options.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<CliSandboxValidationResultDto> ValidateContainerSandboxAsync(CancellationToken cancellationToken = default)
    {
        var startedAt = Stopwatch.StartNew();
        var result = new CliSandboxValidationResultDto
        {
            ConfiguredMode = _options.Mode,
            Provider = nameof(ContainerSandboxSessionProvider),
            ContainerImage = _options.ContainerImage,
            ContainerShell = _options.ContainerShell,
            ContainerWorkingDirectory = _options.ContainerWorkingDirectory,
            DockerContext = _options.DockerContextName ?? string.Empty
        };

        if (!_cliEnvironmentService.IsCommandAvailable(_options.ContainerEngineCommand, out var dockerCommandPath))
        {
            result.FailureReason = $"未找到容器运行时命令：{_options.ContainerEngineCommand}";
            result.ElapsedMilliseconds = startedAt.ElapsedMilliseconds;
            return result;
        }

        result.DockerCommandPath = dockerCommandPath;

        var selectedContextName = await _dockerContextResolver.ResolveAsync(cancellationToken);

        var contextProbe = await RunProcessAsync(
            dockerCommandPath,
            "context show",
            Directory.GetCurrentDirectory(),
            selectedContextName,
            TimeSpan.FromSeconds(15),
            cancellationToken);
        result.DockerContext = contextProbe.StandardOutput.Trim();

        var versionProbe = await RunProcessAsync(
            dockerCommandPath,
            "version --format \"{{.Server.Version}}|{{.Server.Os}}|{{.Server.Arch}}\"",
            Directory.GetCurrentDirectory(),
            selectedContextName,
            TimeSpan.FromSeconds(30),
            cancellationToken);
        if (versionProbe.ExitCode != 0)
        {
            result.ErrorOutput = versionProbe.StandardError;
            result.FailureReason = string.IsNullOrWhiteSpace(versionProbe.StandardError)
                ? "docker version 调用失败。"
                : versionProbe.StandardError.Trim();
            result.ElapsedMilliseconds = startedAt.ElapsedMilliseconds;
            return result;
        }

        result.DockerServer = versionProbe.StandardOutput.Trim();

        var validationRoot = Path.Combine(Path.GetTempPath(), "DevNexus-AI", "sandbox-validation");
        var sessionId = $"sandbox-validation-{Guid.NewGuid():N}";
        var workingDirectory = Path.Combine(validationRoot, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workingDirectory);
        await File.WriteAllTextAsync(
            Path.Combine(workingDirectory, ProbeFileName),
            "sandbox-probe",
            Encoding.UTF8,
            cancellationToken);

        result.HostWorkingDirectory = workingDirectory;

        try
        {
            var lease = await _containerSandboxSessionProvider.AcquireAsync(sessionId, workingDirectory, cancellationToken);
            using var process = new Process { StartInfo = lease.StartInfo };

            process.Start();
            var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);

            await process.StandardInput.WriteLineAsync("pwd");
            await process.StandardInput.WriteLineAsync($"if [ -f \"{NormalizePathForShell(_options.ContainerWorkingDirectory)}/{ProbeFileName}\" ]; then echo {ProbeFileMarker}; else echo DEVNEXUS_PROBE_FILE_MISSING; fi");
            await process.StandardInput.WriteLineAsync($"echo {ProbeSuccessMarker}");
            await process.StandardInput.WriteLineAsync("exit");
            await process.StandardInput.FlushAsync(cancellationToken);

            using var waitCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            waitCts.CancelAfter(TimeSpan.FromSeconds(30));
            await process.WaitForExitAsync(waitCts.Token);

            result.ProbeOutput = await stdoutTask;
            result.ErrorOutput = await stderrTask;
            result.ShellStarted = process.ExitCode == 0 || result.ProbeOutput.Contains(ProbeSuccessMarker, StringComparison.Ordinal);
            result.ProbeFileVisible = result.ProbeOutput.Contains(ProbeFileMarker, StringComparison.Ordinal);
            result.Success = result.ShellStarted && result.ProbeFileVisible;

            if (!result.Success)
            {
                result.FailureReason = BuildFailureReason(result, process.ExitCode);
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "CLI 容器 sandbox 验证失败");
            result.FailureReason = ex.Message;
            result.ErrorOutput = ex.ToString();
            return result;
        }
        finally
        {
            _containerSandboxSessionProvider.Release(sessionId);
            TryDeleteDirectory(workingDirectory);
            result.ElapsedMilliseconds = startedAt.ElapsedMilliseconds;
        }
    }

    private static string BuildFailureReason(CliSandboxValidationResultDto result, int exitCode)
    {
        if (!result.ShellStarted)
        {
            return $"容器 shell 未成功启动，退出码：{exitCode}";
        }

        if (!result.ProbeFileVisible)
        {
            return "容器已启动，但未能在容器内看到宿主机验证文件。当前 docker 上下文可能不是与本机工作目录一致的宿主。";
        }

        return "容器 sandbox 验证失败。";
    }

    private static string NormalizePathForShell(string path)
    {
        return path.Replace('\\', '/').TrimEnd('/');
    }

    private static async Task<ProcessProbeResult> RunProcessAsync(
        string fileName,
        string arguments,
        string workingDirectory,
        string? selectedContextName,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };
        startInfo.Environment.Remove("DOCKER_HOST");
        if (!string.IsNullOrWhiteSpace(selectedContextName))
        {
            startInfo.Environment["DOCKER_CONTEXT"] = selectedContextName;
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
        waitCts.CancelAfter(timeout);
        await process.WaitForExitAsync(waitCts.Token);

        return new ProcessProbeResult(
            process.ExitCode,
            await stdoutTask,
            await stderrTask);
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch
        {
        }
    }

    private sealed record ProcessProbeResult(int ExitCode, string StandardOutput, string StandardError);
}
