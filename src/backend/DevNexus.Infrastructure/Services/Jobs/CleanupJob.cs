// using DevNexus.Domain.Abstractions via GlobalUsings
// using DevNexus.Domain.Entities via GlobalUsings
using DevNexus.Infrastructure.Models;
using DevNexus.Shared.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DevNexus.Infrastructure.Services.Jobs;

/// <summary>
/// 清理任务。
/// 只处理平台托管的过期数据，不扫描或清理用户指定的本机工作目录。
/// </summary>
public class CleanupJob
{
    private readonly IFileStorageService _fileStorageService;
    private readonly ApplicationDbContext _dbContext;
    private readonly ILogger<CleanupJob> _logger;

    public CleanupJob(
        IFileStorageService fileStorageService,
        ApplicationDbContext dbContext,
        ILogger<CleanupJob> logger)
    {
        _fileStorageService = fileStorageService;
        _dbContext = dbContext;
        _logger = logger;
    }

    /// <summary>
    /// 清理过期的已删除/归档 Artifact 对应的 S3 文件
    /// </summary>
    public async Task CleanupExpiredFilesAsync(int daysOld, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation(
                "[Job.Cleanup] Starting | DaysOld={DaysOld}",
                daysOld);

            var cutoffDate = DateTime.UtcNow.AddDays(-daysOld);
            var deletedCount = 0;
            var errorCount = 0;

            // 查询已归档或标记为删除且超过指定天数的 Artifact
            var expiredArtifacts = await _dbContext.Set<Artifact>()
                .Where(a => (a.Status == ArtifactLifecycleStatus.Archived || a.Status == ArtifactLifecycleStatus.Deleted)
                         && a.UpdatedAt < cutoffDate)
                .ToListAsync(cancellationToken);

            _logger.LogInformation(
                "[Job.Cleanup] Found {Count} expired artifacts to clean",
                expiredArtifacts.Count);

            // 遍历并删除对应的 S3 文件（如果 Metadata 中包含 fileUrl）
            foreach (var artifact in expiredArtifacts)
            {
                try
                {
                    // 检查 Metadata 中是否包含文件 URL
                    if (artifact.Metadata != null &&
                        artifact.Metadata.TryGetValue("fileUrl", out var fileUrlObj) &&
                        fileUrlObj is string fileUrl &&
                        !string.IsNullOrEmpty(fileUrl))
                    {
                        // 删除 S3/OSS 上的文件
                        var deleted = await _fileStorageService.DeleteFileAsync(fileUrl, cancellationToken);

                        if (deleted)
                        {
                            _logger.LogDebug(
                                "[Job.Cleanup] Deleted file | ArtifactId={ArtifactId} FileUrl={FileUrl}",
                                artifact.Id,
                                fileUrl);
                            deletedCount++;
                        }
                    }

                    // 从数据库中物理删除记录
                    _dbContext.Set<Artifact>().Remove(artifact);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(
                        ex,
                        "[Job.Cleanup] Failed to delete artifact | ArtifactId={ArtifactId}",
                        artifact.Id);
                    errorCount++;
                }
            }

            // 保存数据库更改
            if (expiredArtifacts.Count > 0)
            {
                await _dbContext.SaveChangesAsync(cancellationToken);
            }

            _logger.LogInformation(
                "[Job.Cleanup] Completed | DaysOld={DaysOld} Deleted={Deleted} Errors={Errors}",
                daysOld,
                deletedCount,
                errorCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "[Job.Cleanup] Failed | DaysOld={DaysOld}",
                daysOld);
            throw;
        }
    }

    /// <summary>
    /// 清理平台内部临时文件。
    /// 注意：该任务只允许处理 Path.GetTempPath()/DevNexus，不得扩展到 HostService 的真实 workingDirectory。
    /// </summary>
    public async Task CleanupTempFilesAsync(CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("[Job.CleanupTemp] Starting");

            var tempPath = Path.Combine(Path.GetTempPath(), "DevNexus");

            if (Directory.Exists(tempPath))
            {
                var files = Directory.GetFiles(tempPath, "*.*", SearchOption.AllDirectories);
                var deletedCount = 0;

                foreach (var file in files)
                {
                    try
                    {
                        var fileInfo = new FileInfo(file);

                        // 删除超过 24 小时的临时文件
                        if (DateTime.UtcNow - fileInfo.CreationTimeUtc > TimeSpan.FromHours(24))
                        {
                            File.Delete(file);
                            deletedCount++;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "[Job.CleanupTemp] Failed to delete file | Path={Path}", file);
                    }
                }

                _logger.LogInformation(
                    "[Job.CleanupTemp] Completed | Deleted={Deleted}",
                    deletedCount);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Job.CleanupTemp] Failed");
            throw;
        }
    }
}
