using System.Diagnostics;
using Microsoft.Extensions.Logging;
using System.Runtime.InteropServices;

namespace DevNexus.Infrastructure.Services.CliTerminal;

/// <summary>
/// CLI 执行环境探测服务
/// 负责识别系统可用终端（PowerShell/Bash/CMD）及其路径
/// </summary>
public class CliEnvironmentService
{
    private readonly ILogger<CliEnvironmentService> _logger;
    private string? _shellPath;
    private string? _defaultArgs;

    public CliEnvironmentService(ILogger<CliEnvironmentService> logger)
    {
        _logger = logger;
        DetectEnvironment();
    }

    /// <summary>
    /// 获取默认 shell 路径
    /// </summary>
    public string GetDefaultShell() => _shellPath ?? (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "powershell.exe" : "/bin/bash");

    /// <summary>
    /// 获取默认 shell 启动参数
    /// </summary>
    public string GetDefaultArguments() => _defaultArgs ?? string.Empty;

    /// <summary>
    /// 检查命令是否存在于当前宿主环境中
    /// </summary>
    /// <param name="command">命令名称或路径</param>
    /// <param name="resolvedPath">解析后的可执行路径</param>
    /// <returns>存在返回 true，否则返回 false</returns>
    public bool IsCommandAvailable(string command, out string resolvedPath)
    {
        if (string.IsNullOrWhiteSpace(command))
        {
            resolvedPath = string.Empty;
            return false;
        }

        command = command.Trim();

        if (Path.IsPathRooted(command))
        {
            return TryResolveCommandPath(command, out resolvedPath);
        }

        if (TrySearchPath(command, out resolvedPath))
        {
            return true;
        }

        return TryResolveWithShell(command, out resolvedPath);
    }

    private void DetectEnvironment()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            // 优先检查 PowerShell Core (pwsh)
            if (TrySearchPath("pwsh.exe", out var pwshPath))
            {
                _shellPath = pwshPath;
                _defaultArgs = "-NoLogo -NonInteractive";
                _logger.LogInformation("探测到 PowerShell Core: {Path}", pwshPath);
            }
            else if (TrySearchPath("powershell.exe", out var psPath))
            {
                _shellPath = psPath;
                _defaultArgs = "-NoLogo -NonInteractive";
                _logger.LogInformation("探测到 Windows PowerShell: {Path}", psPath);
            }
            else
            {
                _shellPath = "cmd.exe";
                _defaultArgs = "/Q /K";
                _logger.LogInformation("未发现 PowerShell，回退到 CMD");
            }
        }
        else
        {
            if (TrySearchPath("bash", out var bashPath))
            {
                _shellPath = bashPath;
                _logger.LogInformation("探测到 Bash: {Path}", bashPath);
            }
            else
            {
                _shellPath = "/bin/sh";
                _logger.LogInformation("回退到标准 SH");
            }
        }
    }

    private bool TrySearchPath(string command, out string path)
    {
        path = command;
        try
        {
            var envPath = Environment.GetEnvironmentVariable("PATH");
            if (string.IsNullOrEmpty(envPath)) return false;

            var paths = envPath.Split(Path.PathSeparator);
            foreach (var p in paths)
            {
                if (string.IsNullOrWhiteSpace(p))
                {
                    continue;
                }

                foreach (var candidate in ExpandCommandCandidates(command))
                {
                    var fullPath = Path.Combine(p.Trim(), candidate);
                    if (File.Exists(fullPath))
                    {
                        path = fullPath;
                        return true;
                    }
                }
            }
            return false;
        }
        catch
        {
            return false;
        }
    }

    private bool TryResolveCommandPath(string command, out string resolvedPath)
    {
        resolvedPath = command;

        foreach (var candidate in ExpandCommandCandidates(command))
        {
            if (File.Exists(candidate))
            {
                resolvedPath = candidate;
                return true;
            }
        }

        return false;
    }

    private bool TryResolveWithShell(string command, out string resolvedPath)
    {
        resolvedPath = command;

        try
        {
            var startInfo = BuildShellResolutionStartInfo(command);
            using var process = new Process { StartInfo = startInfo };

            process.Start();
            var stdOut = process.StandardOutput.ReadToEnd();
            var stdErr = process.StandardError.ReadToEnd();

            if (!process.WaitForExit(3000))
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                }
                catch
                {
                    // 忽略超时清理失败，按未解析处理
                }

                return false;
            }

            if (process.ExitCode != 0)
            {
                if (!string.IsNullOrWhiteSpace(stdErr))
                {
                    _logger.LogDebug("Shell 解析命令失败: {Command}, Error={Error}", command, stdErr.Trim());
                }

                return false;
            }

            var output = stdOut.Trim();
            if (string.IsNullOrWhiteSpace(output))
            {
                return false;
            }

            resolvedPath = output
                .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .FirstOrDefault() ?? command;

            return !string.IsNullOrWhiteSpace(resolvedPath);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Shell 解析命令异常: {Command}", command);
            return false;
        }
    }

    private ProcessStartInfo BuildShellResolutionStartInfo(string command)
    {
        var escapedCommand = EscapeSingleQuotedString(command);

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            var shellPath = GetDefaultShell();
            if (shellPath.EndsWith("cmd.exe", StringComparison.OrdinalIgnoreCase))
            {
                return new ProcessStartInfo
                {
                    FileName = shellPath,
                    Arguments = $"/d /c where.exe {EscapeCmdArgument(command)}",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
            }

            var script = $"$cmd = Get-Command -Name '{escapedCommand}' -CommandType Application,ExternalScript -ErrorAction SilentlyContinue | Select-Object -First 1; if ($null -eq $cmd) {{ exit 1 }}; $cmd.Path";
            return new ProcessStartInfo
            {
                FileName = shellPath,
                Arguments = $"-NoLogo -NoProfile -NonInteractive -Command \"{script.Replace("\"", "\\\"")}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
        }

        var shellCommand = $"command -v -- '{escapedCommand}'";
        return new ProcessStartInfo
        {
            FileName = GetDefaultShell(),
            Arguments = $"-lc \"{shellCommand.Replace("\"", "\\\"")}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
    }

    private static string EscapeSingleQuotedString(string value)
    {
        return value.Replace("'", "''");
    }

    private static string EscapeCmdArgument(string value)
    {
        return value.Contains(' ') ? $"\"{value.Replace("\"", "\\\"")}\"" : value;
    }

    private static IEnumerable<string> ExpandCommandCandidates(string command)
    {
        yield return command;

        if (!OperatingSystem.IsWindows() || !string.IsNullOrEmpty(Path.GetExtension(command)))
        {
            yield break;
        }

        foreach (var extension in GetExecutableExtensions())
        {
            yield return command + extension;
        }
    }

    private static IEnumerable<string> GetExecutableExtensions()
    {
        var pathExt = Environment.GetEnvironmentVariable("PATHEXT");
        if (string.IsNullOrWhiteSpace(pathExt))
        {
            return [".exe", ".cmd", ".bat", ".com", ".ps1"];
        }

        return pathExt
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(static ext => !string.IsNullOrWhiteSpace(ext))
            .Select(static ext => ext.StartsWith('.') ? ext : $".{ext}")
            .Distinct(StringComparer.OrdinalIgnoreCase);
    }
}
