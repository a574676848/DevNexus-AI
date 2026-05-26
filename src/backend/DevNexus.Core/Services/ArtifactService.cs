// using DevNexus.Domain.Abstractions via GlobalUsings
// using DevNexus.Domain.Entities via GlobalUsings
using DevNexus.Domain.Abstractions;
using DevNexus.Shared.DTOs;
using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;

namespace DevNexus.Core.Services;

/// <summary>
/// Artifact 服务实现
/// 支持识别代码块、创建 Artifact、推流到前端
/// </summary>
public class ArtifactService : IArtifactService
{
    private readonly IArtifactRepository _artifactRepository;
    private readonly IChatMessageRepository _chatMessageRepository;
    private readonly ILogger<ArtifactService> _logger;
    
    // 支持的 Artifact 类型及其代码块标识
    private static readonly Dictionary<string, string[]> ArtifactPatterns = new()
    {
        { "Html", new[] { "html", "htm" } },
        { "CSharp", new[] { "csharp", "cs", "c#" } },
        { "Python", new[] { "python", "py" } },
        { "JavaScript", new[] { "javascript", "js", "jsx" } },
        { "TypeScript", new[] { "typescript", "ts", "tsx" } },
        { "Markdown", new[] { "markdown", "md" } },
        { "Json", new[] { "json" } },
        { "Xml", new[] { "xml" } },
        { "Sql", new[] { "sql" } }
    };
    
    public ArtifactService(
        IArtifactRepository artifactRepository,
        IChatMessageRepository chatMessageRepository,
        ILogger<ArtifactService> logger)
    {
        _artifactRepository = artifactRepository;
        _chatMessageRepository = chatMessageRepository;
        _logger = logger;
    }
    
    /// <inheritdoc />
    public async Task<List<ArtifactDto>> ExtractArtifactsAsync(string content, Guid messageId)
    {
        var artifacts = new List<ArtifactDto>();
        
        try
        {
            // 正则表达式匹配 Markdown 代码块: ```language\ncontent\n```
            var codeBlockPattern = @"```(\w+)?\s*\n([\s\S]*?)```";
            var matches = Regex.Matches(content, codeBlockPattern);
            
            foreach (Match match in matches)
            {
                var language = match.Groups[1].Value.ToLowerInvariant();
                var codeContent = match.Groups[2].Value.Trim();
                
                // 跳过空代码块
                if (string.IsNullOrWhiteSpace(codeContent))
                {
                    continue;
                }
                
                // 判断代码块是否足够大，值得作为 Artifact（> 100 字符）
                if (codeContent.Length < 100)
                {
                    continue;
                }
                
                // 确定 Artifact 类型
                var artifactType = DetermineArtifactType(language);
                if (artifactType == null)
                {
                    continue;
                }
                
                // 生成 Artifact 名称
                var artifactName = GenerateArtifactName(artifactType, artifacts.Count + 1);
                
                var artifact = new ArtifactDto
                {
                    ArtifactId = Guid.NewGuid(),
                    Type = artifactType,
                    Name = artifactName,
                    Content = codeContent,
                    MessageId = messageId,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                
                artifacts.Add(artifact);
            }
            
            _logger.LogInformation(
                "[Artifact.Extract] Extracted {Count} artifacts | MessageId={MessageId}",
                artifacts.Count,
                messageId);
            
            return artifacts;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "[Artifact.Extract] Failed to extract artifacts | MessageId={MessageId}",
                messageId);
            return artifacts;
        }
    }
    
    /// <inheritdoc />
    public async Task<ArtifactDto> CreateArtifactAsync(
        ArtifactDto artifact,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // 修复：当 MessageId 为空 GUID 时设置为 null，避免外键约束错误
            Guid? messageId = artifact.MessageId == Guid.Empty ? null : artifact.MessageId;
            
            var entity = new Artifact
            {
                Id = artifact.ArtifactId == Guid.Empty ? Guid.NewGuid() : artifact.ArtifactId,
                SemanticId = artifact.SemanticId,
                Version = artifact.Version,
                BaseVersion = artifact.BaseVersion,
                Type = artifact.Type,
                Name = artifact.Name,
                Content = artifact.Content,
                FileAssetId = artifact.FileAssetId,
                FileVersionId = artifact.FileVersionId,
                Metadata = artifact.Metadata,
                ParentArtifactId = artifact.ParentArtifactId,
                MessageId = messageId,
                SessionId = artifact.SessionId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            
            await _artifactRepository.AddAsync(entity, cancellationToken);
            
            _logger.LogInformation(
                "[Artifact.Create] Artifact created | Id={Id} Type={Type} Name={Name}",
                entity.Id,
                entity.Type,
                entity.Name);
            
            return MapToDto(entity);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "[Artifact.Create] Failed to create artifact | Name={Name}",
                artifact.Name);
            throw;
        }
    }
    
    /// <inheritdoc />
    public async Task<ArtifactDto> UpdateArtifactAsync(
        Guid artifactId,
        string content,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var artifact = await _artifactRepository.GetByIdAsync(artifactId, cancellationToken);
            
            if (artifact == null)
            {
                throw new InvalidOperationException($"工件不存在：{artifactId}");
            }
            
            artifact.Content = content;
            artifact.UpdatedAt = DateTime.UtcNow;
            
            await _artifactRepository.UpdateAsync(artifact, cancellationToken);
            
            _logger.LogInformation(
                "[Artifact.Update] Artifact updated | Id={Id}",
                artifactId);
            
            return MapToDto(artifact);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "[Artifact.Update] Failed to update artifact | Id={Id}",
                artifactId);
            throw;
        }
    }
    
    /// <inheritdoc />
    public async Task<ArtifactDto?> GetArtifactAsync(
        Guid artifactId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var artifact = await _artifactRepository.GetByIdAsync(artifactId, cancellationToken);
            
            return artifact != null ? MapToDto(artifact) : null;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "[Artifact.Get] Failed to get artifact | Id={Id}",
                artifactId);
            return null;
        }
    }
    
    /// <inheritdoc />
    public async Task<List<ArtifactDto>> GetMessageArtifactsAsync(
        Guid messageId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var artifacts = await _artifactRepository.ListByMessageIdAsync(messageId, cancellationToken);
            
            return artifacts.Select(MapToDto).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "[Artifact.GetList] Failed to get artifacts | MessageId={MessageId}",
                messageId);
            return new List<ArtifactDto>();
        }
    }
    
    /// <inheritdoc />
    public async Task<bool> DeleteArtifactAsync(
        Guid artifactId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var deleted = await _artifactRepository.DeleteAsync(artifactId, cancellationToken);
            if (!deleted)
            {
                return false;
            }
            
            _logger.LogInformation(
                "[Artifact.Delete] Artifact deleted | Id={Id}",
                artifactId);
            
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "[Artifact.Delete] Failed to delete artifact | Id={Id}",
                artifactId);
            return false;
        }
    }
    
    /// <inheritdoc />
    public async Task<List<ArtifactDto>> GetSessionArtifactsAsync(
        Guid sessionId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // 1. 先获取会话的所有消息ID
            var messageIds = await _chatMessageRepository.ListIdsBySessionAsync(sessionId, cancellationToken);

            // 2. 查询直接关联 SessionId 或关联这些消息的 Artifact
            var artifacts = await _artifactRepository.ListBySessionAsync(sessionId, messageIds, cancellationToken);
            
            _logger.LogInformation(
                "[Artifact.GetSession] Found {Count} artifacts | SessionId={SessionId}",
                artifacts.Count,
                sessionId);
            
            return artifacts.Select(MapToDto).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "[Artifact.GetSession] Failed to get session artifacts | SessionId={SessionId}",
                sessionId);
            return new List<ArtifactDto>();
        }
    }
    
    /// <inheritdoc />
    public async Task<int> LinkArtifactsToMessageAsync(
        IEnumerable<Guid> artifactIds,
        Guid messageId,
        CancellationToken cancellationToken = default)
    {
        var idList = artifactIds?.ToList();
        if (idList == null || !idList.Any())
        {
            return 0;
        }
        
        try
        {
            var message = await _chatMessageRepository.GetByIdAsync(messageId, cancellationToken);
            if (message == null)
            {
                _logger.LogWarning(
                    "[Artifact.Link] Message not found, skip artifact link | MessageId={MessageId} RequestedIds={RequestedCount}",
                    messageId,
                    idList.Count);
                return 0;
            }

            // 批量更新 MessageId
            var updatedCount = await _artifactRepository.LinkToMessageAsync(
                idList,
                messageId,
                message.ChatSessionId,
                DateTime.UtcNow,
                cancellationToken);
            
            _logger.LogInformation(
                "[Artifact.Link] Linked {Count} artifacts to message | MessageId={MessageId} RequestedIds={RequestedCount}",
                updatedCount,
                messageId,
                idList.Count);
            
            return updatedCount;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "[Artifact.Link] Failed to link artifacts to message | MessageId={MessageId} Ids={Ids}",
                messageId,
                string.Join(",", idList));
            return 0;
        }
    }
    
    /// <inheritdoc />
    public async Task<ArtifactDto> UpdateArtifactMetadataAsync(
        Guid artifactId,
        Dictionary<string, object> metadata,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var artifact = await _artifactRepository.GetByIdAsync(artifactId, cancellationToken);
            
            if (artifact == null)
            {
                throw new InvalidOperationException($"工件不存在：{artifactId}");
            }
            
            // 合并 Metadata（新值覆盖旧值）
            artifact.Metadata ??= new Dictionary<string, object>();
            foreach (var kvp in metadata)
            {
                artifact.Metadata[kvp.Key] = kvp.Value;
            }
            
            artifact.UpdatedAt = DateTime.UtcNow;
            await _artifactRepository.UpdateAsync(artifact, cancellationToken);
            
            _logger.LogInformation(
                "[Artifact.UpdateMetadata] Updated | ArtifactId={ArtifactId} Keys={Keys}",
                artifactId,
                string.Join(", ", metadata.Keys));
            
            return MapToDto(artifact);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "[Artifact.UpdateMetadata] Failed | ArtifactId={ArtifactId}",
                artifactId);
            throw;
        }
    }
    
    private static string? DetermineArtifactType(string language)
    {
        if (string.IsNullOrEmpty(language))
        {
            return null;
        }
        
        foreach (var (type, patterns) in ArtifactPatterns)
        {
            if (patterns.Contains(language))
            {
                return type;
            }
        }
        
        return null;
    }
    
    private static string GenerateArtifactName(string type, int index)
    {
        return $"{type}Document_{index}_{DateTime.UtcNow:yyyyMMdd_HHmmss}";
    }
    
    private static ArtifactDto MapToDto(Artifact entity)
    {
        return new ArtifactDto
        {
            ArtifactId = entity.Id,
            SemanticId = entity.SemanticId,
            Version = entity.Version,
            BaseVersion = entity.BaseVersion,
            Type = entity.Type,
            Name = entity.Name,
            Content = entity.Content,
            FileAssetId = entity.FileAssetId,
            FileVersionId = entity.FileVersionId,
            Metadata = entity.Metadata,
            ParentArtifactId = entity.ParentArtifactId,
            MessageId = entity.MessageId ?? Guid.Empty,
            SessionId = entity.SessionId,
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt
        };
    }
}
