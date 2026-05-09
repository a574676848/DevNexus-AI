using DevNexus.Shared.DTOs;
using DevNexus.Shared.Constants;
using DevNexus.Infrastructure.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DevNexus.Infrastructure.Services.Jobs;

/// <summary>
/// 图片生成后台任务
/// </summary>
public class ImageGenerationJob
{
    private readonly IKernelService _kernelService;
    private readonly IFileStorageService _fileStorageService;
    private readonly ApplicationDbContext _dbContext;
    private readonly IClientNotifier _clientNotifier;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<ImageGenerationJob> _logger;

    public ImageGenerationJob(
        IKernelService kernelService,
        IFileStorageService fileStorageService,
        ApplicationDbContext dbContext,
        IClientNotifier clientNotifier,
        IHttpClientFactory httpClientFactory,
        ILogger<ImageGenerationJob> logger)
    {
        _kernelService = kernelService;
        _fileStorageService = fileStorageService;
        _dbContext = dbContext;
        _clientNotifier = clientNotifier;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    /// <summary>
    /// 执行图片生成任务
    /// </summary>
    public async Task ExecuteAsync(string prompt, int width, int height, Guid conversationId, Guid userId)
    {
        _logger.LogInformation("[ImageGenerationJob] Starting job for conversation {ConversationId}", conversationId);

        await _clientNotifier.NotifyThinkingAsync(
            userId,
            conversationId,
            "🎨 正在生成图片，请稍候...",
            metadata: new Dictionary<string, object> { ["source"] = "ImageGeneration" });

        try
        {
            // 1. 生成图片
            var result = await _kernelService.GenerateImageAsync(
                prompt,
                width,
                height,
                auditScope: new ModelInvocationScopeDto
                {
                    OwnerType = ModelInvocationOwnerTypes.User,
                    OwnerUserId = userId,
                    SessionId = conversationId,
                    SceneCode = ModelInvocationSceneCodes.GenerationImageCreate,
                    SceneCategory = ModelInvocationSceneCategories.UserFacing,
                    ResourceType = ModelInvocationResourceTypes.Session,
                    ResourceId = conversationId.ToString()
                });

            if (!result.Success)
            {
                _logger.LogError("[ImageGenerationJob] Generation failed: {Error}", result.Error);

                // 通知前端生成失败
                await _clientNotifier.NotifyImageGenerationErrorAsync(
                    userId,
                    conversationId,
                    result.Error ?? "图片生成失败，请稍后重试");

                await _clientNotifier.NotifyThinkingAsync(
                    userId,
                    conversationId,
                    $"❌ 图片生成失败: {result.Error ?? "未知错误"}",
                    metadata: new Dictionary<string, object> { ["source"] = "ImageGeneration" });

                return;
            }

            // 2. 转存图片
            string savedImageUrl;
            try
            {
                savedImageUrl = await DownloadAndSaveImageAsync(result.ImageUrl!);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[ImageGenerationJob] Failed to download/save image, using original URL");
                savedImageUrl = result.ImageUrl!;
            }

            // 3. 保存消息到数据库
            var message = new ChatMessage
            {
                Id = Guid.NewGuid(),
                ChatSessionId = conversationId,
                SenderType = ChatConstants.RoleAssistant,
                MessageType = ChatConstants.MessageTypeImage,
                Content = new Dictionary<string, object> { ["text"] = savedImageUrl },
                CreatedAt = DateTime.UtcNow,
                Metadata = new Dictionary<string, object>
                {
                    ["prompt"] = prompt,
                    ["width"] = width,
                    ["height"] = height,
                    ["originalUrl"] = result.ImageUrl ?? string.Empty,
                    ["provider"] = result.ProviderName ?? "unknown"
                }
            };

            await _dbContext.ChatMessages.AddAsync(message);

            // 更新会话最后活动时间
            var session = await _dbContext.ChatSessions.FindAsync(conversationId);
            if (session != null)
            {
                session.UpdatedAt = DateTime.UtcNow;
            }

            await _dbContext.SaveChangesAsync(CancellationToken.None);

            // 4. 推送给客户端
            var messageDto = new ChatMessageDto
            {
                Id = message.Id,
                ChatSessionId = message.ChatSessionId,
                SenderType = message.SenderType,
                MessageType = message.MessageType,
                Content = savedImageUrl,
                CreatedAt = message.CreatedAt,
                Metadata = new Dictionary<string, object>
                {
                    ["markdown"] = $"![Generated Image]({savedImageUrl})"
                }
            };

            await _clientNotifier.NotifyMessageGeneratedAsync(userId, conversationId, messageDto);

            await _clientNotifier.NotifyThinkingAsync(
                userId,
                conversationId,
                "✅ 图片生成完成，已发送到对话中。",
                metadata: new Dictionary<string, object> { ["source"] = "ImageGeneration" });

            _logger.LogInformation("[ImageGenerationJob] Job completed successfully for conversation {ConversationId}", conversationId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ImageGenerationJob] Critical error in job execution");

            // 通知前端发生异常
            try
            {
                await _clientNotifier.NotifyImageGenerationErrorAsync(
                    userId,
                    conversationId,
                    "图片生成过程中发生错误，请稍后重试");

                await _clientNotifier.NotifyThinkingAsync(
                    userId,
                    conversationId,
                    "❌ 图片生成过程中发生错误，请稍后重试。",
                    metadata: new Dictionary<string, object> { ["source"] = "ImageGeneration" });
            }
            catch (Exception notifyEx)
            {
                _logger.LogError(notifyEx, "[ImageGenerationJob] Failed to notify client about error");
            }
        }
    }

    private async Task<string> DownloadAndSaveImageAsync(string imageUrl)
    {
        using var client = _httpClientFactory.CreateClient();
        var imageBytes = await client.GetByteArrayAsync(imageUrl);

        using var stream = new MemoryStream(imageBytes);
        var fileName = $"gen_{Guid.NewGuid()}.png";

        // IFileStorageService.UploadFileAsync returns the public URL
        return await _fileStorageService.UploadFileAsync(stream, fileName, "image/png");
    }
}
