using Microsoft.SemanticKernel;
using System.ComponentModel;
using DevNexus.Core.Abstractions;
using DevNexus.Core.Models.Execution;
using DevNexus.Core.Services.Chat;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Threading.Tasks;

namespace DevNexus.Infrastructure.Services.Plugins;

/// <summary>
/// 代码执行插件 (Phase 3)
/// 允许 LLM 将代码片段保存为临时文件并由结构化宿主服务执行
/// </summary>
public class CodeExecutionPlugin
{
    private readonly IHostStructuredService _hostService;
    private readonly IUserContextAccessor _userContextAccessor;
    private readonly ICliExecutionPolicyService _cliExecutionPolicyService;
    private readonly ILogger<CodeExecutionPlugin> _logger;

    public CodeExecutionPlugin(
        IHostStructuredService hostService,
        IUserContextAccessor userContextAccessor,
        ICliExecutionPolicyService cliExecutionPolicyService,
        ILogger<CodeExecutionPlugin> logger)
    {
        _hostService = hostService;
        _userContextAccessor = userContextAccessor;
        _cliExecutionPolicyService = cliExecutionPolicyService;
        _logger = logger;
    }

    [KernelFunction, Description("在本机工作目录中动态执行代码片段 (Python, Node.js, PowerShell 等)")]
    public async Task<string> RunCodeAsync(
        [Description("编程语言，例如 python, node, pwsh, bash")] string language,
        [Description("要执行的代码内容")] string code,
        [Description("本机工作目录；为空时使用服务默认目录")] string workingDirectory,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(code)) return TaggedExecutionText.Failure("代码内容不能为空。");

        var userId = _userContextAccessor.CurrentUserId;
        if (!userId.HasValue)
        {
            return TaggedExecutionText.Failure("缺少用户上下文，无法执行代码。");
        }

        var effectiveWorkingDirectory = _cliExecutionPolicyService.ResolveWorkingDirectory(userId.Value, workingDirectory);
        if (!Directory.Exists(effectiveWorkingDirectory))
        {
            return TaggedExecutionText.Failure($"指定工作目录不存在或无法访问：{effectiveWorkingDirectory}");
        }

        // ✅ 代码执行开始提醒
        await ThinkingContext.EmitAsync($"⚙️ 正在执行 {language} 代码...");

        var policy = _cliExecutionPolicyService.EvaluateCodeContent(
            language,
            code,
            ChatExecutionContext.CurrentApprovalMode);
        if (!policy.Allowed)
        {
            await ThinkingContext.EmitAsync("🛡️ 当前代码内容命中执行策略，已停止。");
            _logger.LogWarning(
                "[CodeExecution] 策略层拦截代码执行 | 会话={Session} 原因={Reason}",
                _userContextAccessor.CurrentSessionId,
                policy.Message);
            return TaggedExecutionText.SecurityBlocked(policy.Message);
        }

        var ext = language.ToLower() switch
        {
            "python" => ".py",
            "node" or "javascript" => ".js",
            "pwsh" or "powershell" => ".ps1",
            "bash" or "sh" => ".sh",
            _ => ".tmp"
        };

        var fileName = $"exec_{Guid.NewGuid():N}{ext}";
        var fullPath = Path.Combine(effectiveWorkingDirectory, fileName);

        try
        {
            _logger.LogInformation(
                "[CodeExecution] 准备执行 {Lang} 脚本 | 会话={Session} 文件={File}",
                language,
                _userContextAccessor.CurrentSessionId,
                fileName);

            // 1. 写入临时文件
            var writeResult = await _hostService.WriteFileTextResultAsync(fullPath, code, ct);
            if (!writeResult.Succeeded)
            {
                return TaggedExecutionText.Failure(
                    $"无法写入执行脚本: {HostOperationTextFormatter.Format(writeResult)}");
            }

            // 2. 构造执行命令
            var cmd = language.ToLower() switch
            {
                "python" => "python",
                "node" or "javascript" => "node",
                "pwsh" or "powershell" => "pwsh",
                "bash" or "sh" => isWindows() ? "sh" : "bash",
                _ => ""
            };

            if (string.IsNullOrEmpty(cmd))
                return TaggedExecutionText.Failure($"不支持的语言类型: {language}");

            // 3. 执行
            var executionResult = await _hostService.ExecuteCommandResultAsync(
                cmd,
                fileName,
                effectiveWorkingDirectory,
                ct);

            // ✅ 执行完成提醒
            await ThinkingContext.EmitAsync($"✅ {language} 代码执行完成");

            // 4. ✅ 修复：可靠的清理逻辑
            _ = Task.Run(async () => {
                try
                {
                    await Task.Delay(1000);
                    if (File.Exists(fullPath))
                    {
                        File.Delete(fullPath);
                        _logger.LogDebug("[CodeExecution] 临时文件已清理: {File}", fileName);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "[CodeExecution] 清理临时文件失败: {File}", fileName);
                }
            }, CancellationToken.None);

            return HostOperationTextFormatter.FormatCommand(executionResult);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[CodeExecution] 执行失败");
            return TaggedExecutionText.Exception($"代码执行异常: {ex.Message}");
        }
    }
    private bool isWindows() => OperatingSystem.IsWindows();
}
