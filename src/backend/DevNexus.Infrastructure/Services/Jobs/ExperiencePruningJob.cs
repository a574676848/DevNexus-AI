using DevNexus.Core.Abstractions;
using Hangfire;
using Microsoft.Extensions.Logging;

namespace DevNexus.Infrastructure.Services.Jobs;

/// <summary>
/// 每日经验打折衰减任务
/// </summary>
public class ExperiencePruningJob
{
    private readonly IAgentMemoryService _memoryService;
    private readonly ILogger<ExperiencePruningJob> _logger;

    public ExperiencePruningJob(IAgentMemoryService memoryService, ILogger<ExperiencePruningJob> logger)
    {
        _memoryService = memoryService;
        _logger = logger;
    }

    [AutomaticRetry(Attempts = 1, OnAttemptsExceeded = AttemptsExceededAction.Fail)]
    public async Task PruneAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("[PruningJob] 开始执行每日经验修剪与衰减任务...");
        await _memoryService.PruneExperiencesAsync(cancellationToken);
        _logger.LogInformation("[PruningJob] 经验修剪任务完成。");
    }
}
