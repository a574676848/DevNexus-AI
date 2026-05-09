using DevNexus.Core.Abstractions;
using DevNexus.Core.Models.Execution;
using DevNexus.Domain.Abstractions;
using DevNexus.Domain.Entities;
using DevNexus.Shared.DTOs;
using DevNexus.Shared.Enums;
using DevNexus.Infrastructure.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DevNexus.Infrastructure.Services.Files;

/// <summary>
/// 基于数据库持久化的文件任务服务
/// </summary>
public partial class PersistentFileTaskService : IFileTaskService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly ILogger<PersistentFileTaskService> _logger;
    private readonly IServiceScopeFactory _serviceScopeFactory;

    /// <summary>
    /// 构造函数
    /// </summary>
    public PersistentFileTaskService(
        ApplicationDbContext dbContext,
        ILogger<PersistentFileTaskService> logger,
        IServiceScopeFactory serviceScopeFactory)
    {
        _dbContext = dbContext;
        _logger = logger;
        _serviceScopeFactory = serviceScopeFactory;
    }

    /// <inheritdoc />
    public Task<FileTaskIntentDecisionResponse> DecideFileTaskIntentAsync(
        Guid userId,
        FileTaskIntentDecisionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.InputAssetIds.Count == 0)
        {
            throw new InvalidOperationException("至少需要一个输入文件资产");
        }

        var taskType = ResolveIntentTaskType(request.InputAssetIds.Count, request.Instructions);
        var shouldCreate = ShouldCreateFileTask(request.Instructions, request.InputAssetIds.Count);
        var confidence = shouldCreate ? 0.88 : 0.26;
        var reason = shouldCreate
            ? string.IsNullOrWhiteSpace(request.Instructions)
                ? "存在可执行文件资产，默认创建文件准备任务"
                : "当前输入包含明确的文件处理意图，建议创建文件任务"
            : "当前输入更像普通问答，不建议创建文件任务";

        return Task.FromResult(new FileTaskIntentDecisionResponse
        {
            ShouldCreateFileTask = shouldCreate,
            TaskType = taskType,
            Confidence = confidence,
            Reason = reason,
            DecisionSource = "fallback"
        });
    }

    /// <inheritdoc />
    public async Task<FileTaskDto> CreateFileTaskAsync(
        Guid userId,
        CreateFileTaskRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.TaskType))
        {
            throw new InvalidOperationException("任务类型不能为空");
        }

        if (request.InputAssetIds.Count == 0)
        {
            throw new InvalidOperationException("至少需要一个输入文件资产");
        }

        var allAssetIds = request.InputAssetIds
            .Concat(request.TemplateAssetIds)
            .Distinct()
            .ToList();

        var assetCount = await _dbContext.FileAssets
            .CountAsync(
                asset => allAssetIds.Contains(asset.Id) && asset.CreatedBy == userId && !asset.IsDeleted,
                cancellationToken);

        if (assetCount != allAssetIds.Count)
        {
            throw new InvalidOperationException("存在无效的输入文件资产");
        }

        var now = DateTime.UtcNow;
        var task = new FileTask
        {
            Id = Guid.NewGuid(),
            SessionId = request.SessionId,
            TaskType = request.TaskType,
            InputAssetIds = request.InputAssetIds,
            TemplateAssetIds = request.TemplateAssetIds,
            Instructions = request.Instructions,
            Status = FileTaskStatus.Pending,
            Stage = FileTaskStage.Queued,
            StageSummary = "任务已创建，等待后台处理",
            CreatedAt = now,
            UpdatedAt = now,
            CreatedBy = userId,
            UpdatedBy = userId
        };

        _dbContext.FileTasks.Add(task);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "[Files.Task] Created file task. UserId={UserId}, FileTaskId={FileTaskId}, TaskType={TaskType}",
            userId,
            task.Id,
            task.TaskType);

        _ = RunTaskPreparationAsync(task.Id, userId);

        return Map(task);
    }

    /// <inheritdoc />
    public async Task<FileTaskDto> CancelFileTaskAsync(
        Guid userId,
        Guid fileTaskId,
        CancellationToken cancellationToken = default)
    {
        var task = await _dbContext.FileTasks
            .FirstOrDefaultAsync(x => x.Id == fileTaskId && x.CreatedBy == userId, cancellationToken);

        if (task == null || task.IsDeleted)
        {
            throw new InvalidOperationException("文件任务不存在");
        }

        if (task.Status is FileTaskStatus.Completed or FileTaskStatus.Failed or FileTaskStatus.Cancelled)
        {
            throw new InvalidOperationException("当前任务状态不支持取消");
        }

        task.Status = FileTaskStatus.Cancelled;
        task.Stage = FileTaskStage.Cancelled;
        task.StageSummary = "任务已取消";
        task.ErrorSummary = null;
        task.UpdatedAt = DateTime.UtcNow;
        task.UpdatedBy = userId;

        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "[Files.Task] Cancelled file task. UserId={UserId}, FileTaskId={FileTaskId}, TaskType={TaskType}",
            userId,
            task.Id,
            task.TaskType);

        return Map(task);
    }

    /// <inheritdoc />
    public async Task<FileTaskDto?> GetFileTaskAsync(
        Guid userId,
        Guid fileTaskId,
        CancellationToken cancellationToken = default)
    {
        var task = await _dbContext.FileTasks
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == fileTaskId && x.CreatedBy == userId, cancellationToken);

        return task == null || task.IsDeleted ? null : Map(task);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<FileTaskDto>> GetSessionFileTasksAsync(
        Guid userId,
        Guid sessionId,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.FileTasks
            .AsNoTracking()
            .Where(task => task.CreatedBy == userId && task.SessionId == sessionId && !task.IsDeleted)
            .OrderByDescending(task => task.UpdatedAt)
            .Select(task => new FileTaskDto
            {
                FileTaskId = task.Id,
                SessionId = task.SessionId,
                TaskType = task.TaskType,
                InputAssetIds = task.InputAssetIds,
                TemplateAssetIds = task.TemplateAssetIds,
                OutputAssetIds = task.OutputAssetIds,
                Status = task.Status,
                Stage = task.Stage,
                StageSummary = task.StageSummary,
                Instructions = task.Instructions,
                ErrorSummary = task.ErrorSummary,
                CreatedAt = task.CreatedAt,
                UpdatedAt = task.UpdatedAt
            })
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<FileTaskDto> RetryFileTaskAsync(
        Guid userId,
        Guid fileTaskId,
        CancellationToken cancellationToken = default)
    {
        var task = await _dbContext.FileTasks
            .FirstOrDefaultAsync(x => x.Id == fileTaskId && x.CreatedBy == userId, cancellationToken);

        if (task == null || task.IsDeleted)
        {
            throw new InvalidOperationException("文件任务不存在");
        }

        if (task.Status is FileTaskStatus.Pending or FileTaskStatus.Running)
        {
            throw new InvalidOperationException("文件任务仍在执行中，暂时不能重试");
        }

        if (task.Status is not (FileTaskStatus.Failed or FileTaskStatus.Cancelled))
        {
            throw new InvalidOperationException("当前任务状态不支持重试");
        }

        task.Status = FileTaskStatus.Pending;
        task.Stage = FileTaskStage.Queued;
        task.StageSummary = "任务已重新排队，等待再次处理";
        task.ErrorSummary = null;
        task.OutputAssetIds = new List<Guid>();
        task.TaskDirectoryPath = null;
        task.UpdatedAt = DateTime.UtcNow;
        task.UpdatedBy = userId;

        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "[Files.Task] Retried file task. UserId={UserId}, FileTaskId={FileTaskId}, TaskType={TaskType}",
            userId,
            task.Id,
            task.TaskType);

        _ = RunTaskPreparationAsync(task.Id, userId);

        return Map(task);
    }

    private static FileTaskDto Map(FileTask task)
    {
        return new FileTaskDto
        {
            FileTaskId = task.Id,
            SessionId = task.SessionId,
            TaskType = task.TaskType,
            InputAssetIds = task.InputAssetIds,
            TemplateAssetIds = task.TemplateAssetIds,
            OutputAssetIds = task.OutputAssetIds,
            Status = task.Status,
            Stage = task.Stage,
            StageSummary = task.StageSummary,
            Instructions = task.Instructions,
            ErrorSummary = task.ErrorSummary,
            CreatedAt = task.CreatedAt,
            UpdatedAt = task.UpdatedAt
        };
    }

    private async Task RunTaskPreparationAsync(Guid fileTaskId, Guid userId)
    {
        IUserContextAccessor? userContextAccessor = null;

        try
        {
            using var scope = _serviceScopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var storageService = scope.ServiceProvider.GetRequiredService<IFileStorageService>();
            var outputValidationService = scope.ServiceProvider.GetRequiredService<IFileOutputValidationService>();
            var hostService = scope.ServiceProvider.GetRequiredService<IHostStructuredService>();
            var userStoragePathService = scope.ServiceProvider.GetRequiredService<IUserStoragePathService>();
            userContextAccessor = scope.ServiceProvider.GetRequiredService<IUserContextAccessor>();

            var task = await dbContext.FileTasks
                .FirstOrDefaultAsync(x => x.Id == fileTaskId && x.CreatedBy == userId, CancellationToken.None);

            if (task == null || task.IsDeleted)
            {
                return;
            }

            if (await IsTaskCancelledAsync(dbContext, fileTaskId, userId, CancellationToken.None))
            {
                return;
            }

            await UpdateTaskStateAsync(
                dbContext,
                task,
                FileTaskStatus.Running,
                FileTaskStage.PreparingTaskDirectory,
                "正在创建任务目录",
                CancellationToken.None);

            var taskDirectoryPath = FileTaskDirectoryHelper.PrepareTaskDirectory(
                userStoragePathService,
                userId,
                fileTaskId);

            task.TaskDirectoryPath = taskDirectoryPath;
            if (await IsTaskCancelledAsync(dbContext, fileTaskId, userId, CancellationToken.None))
            {
                return;
            }

            await UpdateTaskStateAsync(
                dbContext,
                task,
                FileTaskStatus.Running,
                FileTaskStage.StagingAssets,
                "正在准备输入文件和模板文件",
                CancellationToken.None);

            var stagedInputs = await FileTaskDirectoryHelper.StageAssetsAsync(
                dbContext,
                task.InputAssetIds,
                taskDirectoryPath,
                "inputs",
                storageService,
                CancellationToken.None);

            var stagedTemplates = await FileTaskDirectoryHelper.StageAssetsAsync(
                dbContext,
                task.TemplateAssetIds,
                taskDirectoryPath,
                "templates",
                storageService,
                CancellationToken.None);

            await FileTaskDirectoryHelper.WriteTaskManifestAsync(
                task,
                taskDirectoryPath,
                stagedInputs,
                stagedTemplates);

            if (await IsTaskCancelledAsync(dbContext, fileTaskId, userId, CancellationToken.None))
            {
                return;
            }

            await UpdateTaskStateAsync(
                dbContext,
                task,
                FileTaskStatus.Running,
                FileTaskStage.ExecutingScript,
                "正在执行文件处理脚本",
                CancellationToken.None);

            userContextAccessor.CurrentUserId = userId;
            userContextAccessor.CurrentSessionId = task.SessionId?.ToString();

            var executionResult = await ExecuteTaskScriptAsync(task, taskDirectoryPath, hostService);

            if (await IsTaskCancelledAsync(dbContext, fileTaskId, userId, CancellationToken.None))
            {
                return;
            }

            await UpdateTaskStateAsync(
                dbContext,
                task,
                FileTaskStatus.Running,
                FileTaskStage.ValidatingOutputs,
                "正在验证输出文件",
                CancellationToken.None);

            var validationResult = await outputValidationService.ValidateAsync(executionResult.GeneratedFiles, CancellationToken.None);
            if (!validationResult.IsValid)
            {
                throw new InvalidOperationException(validationResult.Summary);
            }

            if (await IsTaskCancelledAsync(dbContext, fileTaskId, userId, CancellationToken.None))
            {
                return;
            }

            await UpdateTaskStateAsync(
                dbContext,
                task,
                FileTaskStatus.Running,
                FileTaskStage.PublishingOutputs,
                "正在发布结果文件",
                CancellationToken.None);

            var outputAssetIds = await FileTaskOutputPublisher.PublishGeneratedFilesAsync(
                dbContext,
                task,
                userId,
                executionResult.GeneratedFiles,
                validationResult,
                executionResult.FallbackUsed,
                executionResult.RunnerUsed,
                storageService,
                CancellationToken.None);

            if (await IsTaskCancelledAsync(dbContext, fileTaskId, userId, CancellationToken.None))
            {
                return;
            }

            task.OutputAssetIds = outputAssetIds;
            task.Status = FileTaskStatus.Completed;
            task.Stage = FileTaskStage.Completed;
            task.StageSummary = outputAssetIds.Count > 0
                ? $"已生成并发布 {outputAssetIds.Count} 个结果文件"
                : "任务已完成";
            task.ErrorSummary = null;
            task.UpdatedAt = DateTime.UtcNow;
            await dbContext.SaveChangesAsync(CancellationToken.None);

            _logger.LogInformation(
                "[Files.Task] Prepared task directory. UserId={UserId}, FileTaskId={FileTaskId}, TaskDirectoryPath={TaskDirectoryPath}",
                userId,
                fileTaskId,
                taskDirectoryPath);
        }
        catch (Exception ex)
        {
            await PersistFailureAsync(fileTaskId, userId, ex);

            _logger.LogError(
                ex,
                "[Files.Task] Failed to prepare task directory. UserId={UserId}, FileTaskId={FileTaskId}",
                userId,
                fileTaskId);
        }
        finally
        {
            if (userContextAccessor != null)
            {
                userContextAccessor.CurrentUserId = null;
                userContextAccessor.CurrentSessionId = null;
            }
        }
    }

    private static async Task UpdateTaskStateAsync(
        ApplicationDbContext dbContext,
        FileTask task,
        FileTaskStatus status,
        FileTaskStage stage,
        string? stageSummary,
        CancellationToken cancellationToken)
    {
        task.Status = status;
        task.Stage = stage;
        task.StageSummary = stageSummary;
        task.UpdatedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static async Task<bool> IsTaskCancelledAsync(
        ApplicationDbContext dbContext,
        Guid fileTaskId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var status = await dbContext.FileTasks
            .AsNoTracking()
            .Where(task => task.Id == fileTaskId && task.CreatedBy == userId && !task.IsDeleted)
            .Select(task => task.Status)
            .FirstOrDefaultAsync(cancellationToken);

        return status == FileTaskStatus.Cancelled;
    }

    private async Task<FileTaskExecutionResult> ExecuteTaskScriptAsync(
        FileTask task,
        string taskDirectoryPath,
        IHostStructuredService hostService)
    {
        var scriptPath = await FileTaskExecutionScriptBuilder.WriteExecutionScriptAsync(task, taskDirectoryPath);
        var arguments = $"-NoProfile -ExecutionPolicy Bypass -File \"{scriptPath}\"";
        var executionResult = await hostService.ExecuteCommandResultAsync("pwsh", arguments, taskDirectoryPath);

        if (!executionResult.Succeeded)
        {
            throw new InvalidOperationException(
                $"任务执行失败: {HostOperationTextFormatter.FormatCommand(executionResult)}");
        }

        var outputPath = Path.Combine(taskDirectoryPath, "outputs");
        var outputFiles = Directory.GetFiles(outputPath, "*", SearchOption.AllDirectories)
            .Where(file => !file.EndsWith(".ps1", StringComparison.OrdinalIgnoreCase))
            .OrderBy(file => file, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (outputFiles.Count == 0)
        {
            throw new InvalidOperationException("任务执行完成，但未生成任何输出文件");
        }

        return new FileTaskExecutionResult(
            outputFiles,
            ParseRunnerUsed(executionResult.Output),
            ParseFallbackUsed(executionResult.Output));
    }

    private static string? ParseRunnerUsed(string executionResult)
    {
        const string marker = "Runner=";
        var startIndex = executionResult.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (startIndex < 0)
        {
            return null;
        }

        startIndex += marker.Length;
        var endIndex = executionResult.IndexOf(';', startIndex);
        var value = endIndex >= 0
            ? executionResult[startIndex..endIndex]
            : executionResult[startIndex..];

        var runnerUsed = value.Trim();
        return string.IsNullOrWhiteSpace(runnerUsed) ? null : runnerUsed;
    }

    private static bool ParseFallbackUsed(string executionResult)
    {
        const string marker = "Fallback=";
        var startIndex = executionResult.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (startIndex < 0)
        {
            return false;
        }

        startIndex += marker.Length;
        var endIndex = executionResult.IndexOfAny([';', '\r', '\n'], startIndex);
        var value = endIndex >= 0
            ? executionResult[startIndex..endIndex]
            : executionResult[startIndex..];

        return bool.TryParse(value.Trim(), out var fallbackUsed) && fallbackUsed;
    }

    private sealed record FileTaskExecutionResult(
        IReadOnlyList<string> GeneratedFiles,
        string? RunnerUsed,
        bool FallbackUsed);

    private static bool ShouldCreateFileTask(string? instructions, int inputCount)
    {
        if (inputCount <= 0)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(instructions))
        {
            return true;
        }

        var text = instructions.Trim();
        if (text.Length < 4)
        {
            return false;
        }

        var normalized = text.ToLowerInvariant();
        var questionMarkers = new[]
        {
            "为什么", "怎么", "如何", "what", "why", "how", "explain", "分析", "解释"
        };

        if (questionMarkers.Any(marker => normalized.StartsWith(marker))
            && !normalized.Contains("生成")
            && !normalized.Contains("导出")
            && !normalized.Contains("整理")
            && !normalized.Contains("转换")
            && !normalized.Contains("提取")
            && !normalized.Contains("merge")
            && !normalized.Contains("convert"))
        {
            return false;
        }

        var actionMarkers = new[]
        {
            "生成", "导出", "整理", "转换", "提取", "合并", "拆分", "汇总", "重命名",
            "generate", "export", "convert", "extract", "merge", "split", "transform", "rename"
        };

        return actionMarkers.Any(marker => normalized.Contains(marker)) || inputCount > 0;
    }

    private static string ResolveIntentTaskType(int inputCount, string? instructions)
    {
        if (string.IsNullOrWhiteSpace(instructions))
        {
            return inputCount > 1 ? "multi-file-preparation" : "single-file-preparation";
        }

        return "chat-file-orchestration";
    }

    private async Task PersistFailureAsync(Guid fileTaskId, Guid userId, Exception ex)
    {
        try
        {
            using var scope = _serviceScopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var task = await dbContext.FileTasks
                .FirstOrDefaultAsync(x => x.Id == fileTaskId && x.CreatedBy == userId, CancellationToken.None);

            if (task == null)
            {
                return;
            }

            task.Status = FileTaskStatus.Failed;
            if (task.Stage == FileTaskStage.Queued)
            {
                task.Stage = FileTaskStage.Failed;
            }

            task.StageSummary = ex.Message;
            task.ErrorSummary = ex.Message;
            task.UpdatedAt = DateTime.UtcNow;
            await dbContext.SaveChangesAsync(CancellationToken.None);
        }
        catch (Exception updateException)
        {
            _logger.LogError(
                updateException,
                "[Files.Task] Failed to persist task failure state. UserId={UserId}, FileTaskId={FileTaskId}",
                userId,
                fileTaskId);
        }
    }
}
