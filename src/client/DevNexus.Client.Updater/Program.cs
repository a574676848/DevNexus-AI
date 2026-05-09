using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace DevNexus.Client.Updater;

internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        var options = ParseArguments(args);
        if (options == null)
        {
            return 1;
        }

        var logPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "DevNexus",
            "Updates",
            "updater.log");
        var resultPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "DevNexus",
            "Updates",
            "install-result.json");

        Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);

        try
        {
            await LogAsync(logPath, $"启动 Updater | Installer={options.InstallerPath} ParentPid={options.ParentProcessId}");

            if (!File.Exists(options.InstallerPath))
            {
                await LogAsync(logPath, "安装包不存在，退出。");
                return 2;
            }

            await WaitForParentExitAsync(options.ParentProcessId, logPath);

            var process = Process.Start(new ProcessStartInfo
            {
                FileName = options.InstallerPath,
                Arguments = "/VERYSILENT /NORESTART /SUPPRESSMSGBOXES",
                UseShellExecute = true,
                Verb = "runas"
            });

            if (process == null)
            {
                await LogAsync(logPath, "安装器启动失败。");
                return 3;
            }

            await LogAsync(logPath, $"安装器已启动 | Pid={process.Id}");
            await process.WaitForExitAsync();
            await WriteResultAsync(resultPath, process.ExitCode == 0 ? "success" : "failed", process.ExitCode, process.ExitCode == 0 ? null : $"安装器退出码 {process.ExitCode}");
            await LogAsync(logPath, $"安装器已结束 | ExitCode={process.ExitCode}");
            return process.ExitCode == 0 ? 0 : 3;
        }
        catch (Exception ex)
        {
            await WriteResultAsync(resultPath, "failed", 4, ex.Message);
            await LogAsync(logPath, $"Updater 异常: {ex}");
            return 4;
        }
    }

    private static async Task WaitForParentExitAsync(int parentPid, string logPath)
    {
        if (parentPid <= 0)
        {
            await Task.Delay(1500);
            return;
        }

        try
        {
            using var process = Process.GetProcessById(parentPid);
            await LogAsync(logPath, $"等待主程序退出 | Pid={parentPid}");
            await process.WaitForExitAsync();
            await Task.Delay(800);
        }
        catch
        {
            await LogAsync(logPath, $"主程序进程不存在，直接继续 | Pid={parentPid}");
        }
    }

    private static UpdaterOptions? ParseArguments(IReadOnlyList<string> args)
    {
        string? installerPath = null;
        var parentPid = 0;

        for (var index = 0; index < args.Count; index++)
        {
            switch (args[index])
            {
                case "--installer" when index + 1 < args.Count:
                    installerPath = args[++index];
                    break;
                case "--parent-pid" when index + 1 < args.Count && int.TryParse(args[index + 1], out var parsedPid):
                    parentPid = parsedPid;
                    index++;
                    break;
            }
        }

        if (string.IsNullOrWhiteSpace(installerPath))
        {
            return null;
        }

        return new UpdaterOptions(installerPath, parentPid);
    }

    private static Task LogAsync(string logPath, string message)
    {
        var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}{Environment.NewLine}";
        return File.AppendAllTextAsync(logPath, line, Encoding.UTF8);
    }

    private static async Task WriteResultAsync(string resultPath, string result, int exitCode, string? errorMessage)
    {
        var payload = JsonSerializer.Serialize(new InstallResultPayload
        {
            Result = result,
            ExitCode = exitCode,
            ErrorMessage = errorMessage,
            CompletedAtUtc = DateTime.UtcNow
        });
        await File.WriteAllTextAsync(resultPath, payload, Encoding.UTF8);
    }

    private sealed record UpdaterOptions(string InstallerPath, int ParentProcessId);

    private sealed class InstallResultPayload
    {
        public string Result { get; set; } = string.Empty;
        public int ExitCode { get; set; }
        public string? ErrorMessage { get; set; }
        public DateTime CompletedAtUtc { get; set; }
    }
}
