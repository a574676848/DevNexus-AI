using DevNexus.Domain.Abstractions;
using DevNexus.Domain.Entities;
using DevNexus.Shared.DTOs;
using DevNexus.Infrastructure.Models;
using Microsoft.EntityFrameworkCore;

namespace DevNexus.Infrastructure.Services.Files;

/// <summary>
/// 基于内存的文件资产服务
/// </summary>
public class PersistentFileAssetService : IFileAssetService
{
    private readonly ApplicationDbContext _dbContext;

    /// <summary>
    /// 构造函数
    /// </summary>
    public PersistentFileAssetService(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc />
    public async Task<FileAssetDto?> GetFileAssetAsync(
        Guid userId,
        Guid fileAssetId,
        CancellationToken cancellationToken = default)
    {
        var asset = await _dbContext.FileAssets
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == fileAssetId && x.CreatedBy == userId, cancellationToken);

        if (asset == null || asset.CreatedBy != userId || asset.IsDeleted)
        {
            return null;
        }

        return Map(asset);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<FileAssetDto>> GetSessionFileAssetsAsync(
        Guid userId,
        Guid sessionId,
        CancellationToken cancellationToken = default)
    {
        var assets = await _dbContext.FileAssets
            .AsNoTracking()
            .Where(asset => asset.CreatedBy == userId && asset.SessionId == sessionId && !asset.IsDeleted)
            .OrderByDescending(asset => asset.UpdatedAt)
            .ToListAsync(cancellationToken);

        return assets
            .Select(Map)
            .ToList();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<FileAssetDto>> GetFileAssetsByIdsAsync(
        Guid userId,
        IReadOnlyCollection<Guid> fileAssetIds,
        CancellationToken cancellationToken = default)
    {
        if (fileAssetIds.Count == 0)
        {
            return Array.Empty<FileAssetDto>();
        }

        var normalizedIds = fileAssetIds
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToList();

        if (normalizedIds.Count == 0)
        {
            return Array.Empty<FileAssetDto>();
        }

        var assets = await _dbContext.FileAssets
            .AsNoTracking()
            .Where(asset => asset.CreatedBy == userId && normalizedIds.Contains(asset.Id) && !asset.IsDeleted)
            .ToListAsync(cancellationToken);

        return assets
            .OrderByDescending(asset => asset.UpdatedAt)
            .Select(Map)
            .ToList();
    }

    private static FileAssetDto Map(FileAsset asset)
    {
        return new FileAssetDto
        {
            FileAssetId = asset.Id,
            SessionId = asset.SessionId,
            CurrentVersionId = asset.CurrentVersionId,
            OriginalFileName = asset.OriginalFileName,
            Extension = asset.Extension,
            ContentType = asset.ContentType,
            StorageProvider = asset.StorageProvider,
            FileUrl = asset.FileUrl,
            ObjectKey = asset.ObjectKey,
            SizeBytes = asset.SizeBytes,
            Status = asset.Status,
            SourceType = asset.SourceType,
            Metadata = asset.Metadata,
            CreatedAt = asset.CreatedAt,
            UpdatedAt = asset.UpdatedAt
        };
    }
}
