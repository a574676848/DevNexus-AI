using DevNexus.Domain.Abstractions;
using DevNexus.Domain.Entities;
using DevNexus.Shared.DTOs;
using DevNexus.Shared.Enums;
using DevNexus.Infrastructure.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DevNexus.Infrastructure.Services.Files;

/// <summary>
/// 基于内存的上传会话服务
/// </summary>
public class PersistentUploadSessionService : IUploadSessionService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IFileStorageService _fileStorageService;
    private readonly ILogger<PersistentUploadSessionService> _logger;

    /// <summary>
    /// 构造函数
    /// </summary>
    public PersistentUploadSessionService(
        ApplicationDbContext dbContext,
        IFileStorageService fileStorageService,
        ILogger<PersistentUploadSessionService> logger)
    {
        _dbContext = dbContext;
        _fileStorageService = fileStorageService;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<CreateUploadSessionResponse> CreateUploadSessionAsync(
        Guid userId,
        CreateUploadSessionRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.FileName))
        {
            throw new InvalidOperationException("文件名不能为空");
        }

        var folder = request.SessionId.HasValue
            ? $"uploads/{userId}/{request.SessionId.Value:N}"
            : $"uploads/{userId}";

        var presignedUpload = await _fileStorageService.GeneratePresignedUploadAsync(
            request.FileName,
            request.ContentType,
            folder,
            cancellationToken);

        var now = DateTime.UtcNow;
        var assetId = Guid.NewGuid();
        var versionId = Guid.NewGuid();
        var uploadSessionId = Guid.NewGuid();

        var asset = new FileAsset
        {
            Id = assetId,
            SessionId = request.SessionId,
            CurrentVersionId = versionId,
            OriginalFileName = request.FileName,
            Extension = Path.GetExtension(request.FileName),
            ContentType = request.ContentType,
            StorageProvider = _fileStorageService.Provider,
            FileUrl = presignedUpload.FileUrl,
            ObjectKey = presignedUpload.ObjectKey,
            Status = FileAssetStatus.PendingUpload,
            SourceType = request.SourceType,
            CreatedAt = now,
            UpdatedAt = now,
            CreatedBy = userId,
            UpdatedBy = userId,
            Metadata = new Dictionary<string, object>
            {
                ["uploadMethod"] = presignedUpload.UploadMethod
            }
        };

        var version = new FileVersion
        {
            Id = versionId,
            FileAssetId = assetId,
            VersionNumber = 1,
            ObjectKey = presignedUpload.ObjectKey,
            FileUrl = presignedUpload.FileUrl,
            CreatedAt = now,
            UpdatedAt = now,
            CreatedBy = userId,
            UpdatedBy = userId
        };

        var uploadSession = new UploadSession
        {
            Id = uploadSessionId,
            FileAssetId = assetId,
            SessionId = request.SessionId,
            FileName = request.FileName,
            ContentType = request.ContentType,
            FileUrl = presignedUpload.FileUrl,
            ObjectKey = presignedUpload.ObjectKey,
            UploadUrl = presignedUpload.UploadUrl,
            UploadMethod = presignedUpload.UploadMethod,
            Status = UploadSessionStatus.Created,
            ExpectedSizeBytes = request.ExpectedSizeBytes,
            ExpiresAt = presignedUpload.ExpiresAt,
            CreatedAt = now,
            UpdatedAt = now,
            CreatedBy = userId,
            UpdatedBy = userId
        };

        _dbContext.FileAssets.Add(asset);
        _dbContext.FileVersions.Add(version);
        _dbContext.UploadSessions.Add(uploadSession);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "[Files.Upload] Created upload session. UserId={UserId}, UploadSessionId={UploadSessionId}, FileAssetId={FileAssetId}",
            userId,
            uploadSession.Id,
            asset.Id);

        return new CreateUploadSessionResponse
        {
            UploadSession = Map(uploadSession)
        };
    }

    /// <inheritdoc />
    public async Task<FinalizeUploadResponse> FinalizeUploadAsync(
        Guid userId,
        FinalizeUploadRequest request,
        CancellationToken cancellationToken = default)
    {
        var uploadSession = await _dbContext.UploadSessions
            .FirstOrDefaultAsync(x => x.Id == request.UploadSessionId && x.CreatedBy == userId, cancellationToken);

        if (uploadSession == null || uploadSession.IsDeleted)
        {
            throw new InvalidOperationException("上传会话不存在");
        }

        var asset = await _dbContext.FileAssets
            .FirstOrDefaultAsync(x => x.Id == uploadSession.FileAssetId && x.CreatedBy == userId, cancellationToken);

        if (asset == null || asset.IsDeleted)
        {
            throw new InvalidOperationException("文件资产不存在");
        }

        if (uploadSession.ExpiresAt <= DateTime.UtcNow)
        {
            uploadSession.Status = UploadSessionStatus.Expired;
            uploadSession.UpdatedAt = DateTime.UtcNow;
            asset.Status = FileAssetStatus.Failed;
            asset.UpdatedAt = DateTime.UtcNow;
            throw new InvalidOperationException("上传会话已过期");
        }

        var fileExists = await _fileStorageService.FileExistsAsync(uploadSession.FileUrl, cancellationToken);
        if (!fileExists)
        {
            uploadSession.Status = UploadSessionStatus.Failed;
            uploadSession.UpdatedAt = DateTime.UtcNow;
            asset.Status = FileAssetStatus.Failed;
            asset.UpdatedAt = DateTime.UtcNow;
            throw new InvalidOperationException("文件尚未上传完成或存储对象不可见");
        }

        var actualSize = request.SizeBytes ?? await _fileStorageService.GetFileSizeAsync(uploadSession.FileUrl, cancellationToken);
        if (uploadSession.ExpectedSizeBytes.HasValue &&
            actualSize > 0 &&
            uploadSession.ExpectedSizeBytes.Value != actualSize)
        {
            throw new InvalidOperationException("上传文件大小与预期不一致");
        }

        var now = DateTime.UtcNow;
        uploadSession.Status = UploadSessionStatus.Finalized;
        uploadSession.UpdatedAt = now;
        uploadSession.UpdatedBy = userId;

        asset.SizeBytes = actualSize;
        asset.Status = FileAssetStatus.Uploaded;
        asset.UpdatedAt = now;
        asset.UpdatedBy = userId;

        var version = await _dbContext.FileVersions
            .FirstOrDefaultAsync(x => x.Id == asset.CurrentVersionId, cancellationToken);

        if (version != null)
        {
            version.SizeBytes = actualSize;
            version.UpdatedAt = now;
            version.UpdatedBy = userId;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "[Files.Upload] Finalized upload session. UserId={UserId}, UploadSessionId={UploadSessionId}, FileAssetId={FileAssetId}, SizeBytes={SizeBytes}",
            userId,
            uploadSession.Id,
            asset.Id,
            actualSize);

        return new FinalizeUploadResponse
        {
            UploadSession = Map(uploadSession),
            FileAsset = Map(asset)
        };
    }

    /// <inheritdoc />
    public async Task<UploadSessionDto?> GetUploadSessionAsync(
        Guid userId,
        Guid uploadSessionId,
        CancellationToken cancellationToken = default)
    {
        var uploadSession = await _dbContext.UploadSessions
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == uploadSessionId && x.CreatedBy == userId, cancellationToken);

        if (uploadSession == null || uploadSession.CreatedBy != userId || uploadSession.IsDeleted)
        {
            return null;
        }

        return Map(uploadSession);
    }

    private static UploadSessionDto Map(UploadSession uploadSession)
    {
        return new UploadSessionDto
        {
            UploadSessionId = uploadSession.Id,
            FileAssetId = uploadSession.FileAssetId,
            SessionId = uploadSession.SessionId,
            FileName = uploadSession.FileName,
            ContentType = uploadSession.ContentType,
            FileUrl = uploadSession.FileUrl,
            ObjectKey = uploadSession.ObjectKey,
            UploadUrl = uploadSession.UploadUrl,
            UploadMethod = uploadSession.UploadMethod,
            Status = uploadSession.Status,
            ExpectedSizeBytes = uploadSession.ExpectedSizeBytes,
            ExpiresAt = uploadSession.ExpiresAt,
            CreatedAt = uploadSession.CreatedAt,
            UpdatedAt = uploadSession.UpdatedAt
        };
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
