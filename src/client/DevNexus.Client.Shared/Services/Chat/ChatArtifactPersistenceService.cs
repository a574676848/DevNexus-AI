using DevNexus.Client.Shared.Abstractions;
using DevNexus.Shared.Constants;
using DevNexus.Shared.DTOs;

namespace DevNexus.Client.Shared.Services.Chat;

public sealed class ChatArtifactPersistenceService
{
    private readonly IApiService _apiService;
    private readonly IRemoteLogService _remoteLog;

    public ChatArtifactPersistenceService(
        IApiService apiService,
        IRemoteLogService remoteLog)
    {
        _apiService = apiService;
        _remoteLog = remoteLog;
    }

    public async Task SaveArtifactsToDbAsync(
        Guid sessionId,
        Guid messageId,
        List<ArtifactDto> completedArtifacts,
        List<BlockDto> chartBlocks,
        List<BlockDto> interactiveBlocks,
        ArtifactDto? currentArtifact)
    {
        foreach (var artifact in completedArtifacts)
        {
            if (!string.IsNullOrEmpty(artifact.Content))
            {
                await CreateArtifactSafeAsync(new CreateArtifactRequestDto
                {
                    SemanticId = artifact.SemanticId,
                    Version = artifact.Version,
                    ParentArtifactId = artifact.ParentArtifactId,
                    Type = artifact.Type ?? "code",
                    Name = artifact.Name ?? "AI 生成的代码",
                    Content = artifact.Content,
                    SessionId = sessionId,
                    MessageId = messageId
                });
            }
        }

        foreach (var chartBlock in chartBlocks)
        {
            if (!string.IsNullOrEmpty(chartBlock.Content))
            {
                var chartTitle = ChatMessageMetadataReader.GetString(chartBlock.Metadata, ArtifactBlockMetadataConstants.Title)
                    ?? ArtifactBlockMetadataConstants.DefaultChartTitle;
                await CreateArtifactSafeAsync(new CreateArtifactRequestDto
                {
                    SemanticId = chartBlock.ArtifactId,
                    Version = chartBlock.Version,
                    Type = ArtifactBlockMetadataConstants.TypeChart,
                    Name = chartTitle,
                    Content = chartBlock.Content,
                    SessionId = sessionId,
                    MessageId = messageId
                });
            }
        }

        foreach (var interactiveBlock in interactiveBlocks)
        {
            if (!string.IsNullOrEmpty(interactiveBlock.Content))
            {
                var cardType = ToolBlockMetadataConstants.NormalizeCardType(
                    ChatMessageMetadataReader.GetString(interactiveBlock.Metadata, ToolBlockMetadataConstants.CardType));
                var artifactType = $"interactive-{cardType}";
                var cardName = cardType switch
                {
                    ToolBlockMetadataConstants.CardTypeCommand => "命令执行",
                    ToolBlockMetadataConstants.CardTypeSql => "SQL 查询",
                    ToolBlockMetadataConstants.CardTypeScript => "脚本执行",
                    ToolBlockMetadataConstants.CardTypeSearch => "搜索结果",
                    ToolBlockMetadataConstants.CardTypeAdvancedSearch => "高级搜索",
                    ToolBlockMetadataConstants.CardTypeWebpage => "网页阅读",
                    ToolBlockMetadataConstants.CardTypeFile => "文件操作",
                    _ => "交互卡片"
                };

                var artifactMetadata = new Dictionary<string, object>
                {
                    [ToolBlockMetadataConstants.CardType] = cardType,
                    [ToolBlockMetadataConstants.IsProcessed] = false,
                    [ToolBlockMetadataConstants.IsApproved] = false
                };

                if (interactiveBlock.Metadata != null)
                {
                    foreach (var kvp in interactiveBlock.Metadata)
                    {
                        if (!artifactMetadata.ContainsKey(kvp.Key))
                        {
                            artifactMetadata[kvp.Key] = kvp.Value;
                        }
                    }
                }

                var createdArtifact = await CreateArtifactSafeAsync(new CreateArtifactRequestDto
                {
                    SemanticId = interactiveBlock.ArtifactId,
                    Version = interactiveBlock.Version,
                    Type = artifactType,
                    Name = cardName,
                    Content = interactiveBlock.Content,
                    SessionId = sessionId,
                    MessageId = messageId,
                    Metadata = artifactMetadata
                });

                if (createdArtifact != null)
                {
                    interactiveBlock.Metadata ??= new Dictionary<string, object>();
                    interactiveBlock.Metadata[ArtifactBlockMetadataConstants.ArtifactId] = createdArtifact.ArtifactId;
                }
            }
        }

        if (currentArtifact != null && !string.IsNullOrEmpty(currentArtifact.Content))
        {
            await CreateArtifactSafeAsync(new CreateArtifactRequestDto
            {
                SemanticId = currentArtifact.SemanticId,
                Version = currentArtifact.Version,
                ParentArtifactId = currentArtifact.ParentArtifactId,
                Type = currentArtifact.Type ?? "code",
                Name = currentArtifact.Name ?? "AI 生成的代码",
                Content = currentArtifact.Content,
                SessionId = sessionId,
                MessageId = messageId
            });
        }
    }

    private async Task<ArtifactDto?> CreateArtifactSafeAsync(CreateArtifactRequestDto dto)
    {
        try
        {
            return await _apiService.CreateArtifactAsync(dto);
        }
        catch (Exception ex)
        {
            await _remoteLog.LogErrorAsync(ex, "ChatMessageProcessor.CreateArtifactSafeAsync", new Dictionary<string, object?>
            {
                ["ArtifactName"] = dto.Name,
                ["SessionId"] = dto.SessionId
            });
            return null;
        }
    }
}