// using DevNexus.Domain.Abstractions via GlobalUsings
using DevNexus.Core.Abstractions;
using DevNexus.Shared.DTOs;
using Hangfire;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using System.ComponentModel;
using System.Text.Json;

namespace DevNexus.Infrastructure.Services.Plugins;

/// <summary>
/// 文生图插件 (Semantic Kernel Plugin)
/// 通过 Function Calling 调用图像生成 API
/// </summary>
public class ImageGenerationPlugin
{
    private readonly Hangfire.IBackgroundJobClient _backgroundJobClient;
    private readonly IKernelService _kernelService;
    private readonly IFileStorageService _fileStorageService;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<ImageGenerationPlugin> _logger;
    private Guid _conversationId;
    private Guid _userId;

    public ImageGenerationPlugin(
        Hangfire.IBackgroundJobClient backgroundJobClient,
        IKernelService kernelService,
        IFileStorageService fileStorageService,
        IHttpClientFactory httpClientFactory,
        ILogger<ImageGenerationPlugin> logger)
    {
        _backgroundJobClient = backgroundJobClient ?? throw new ArgumentNullException(nameof(backgroundJobClient));
        _kernelService = kernelService ?? throw new ArgumentNullException(nameof(kernelService));
        _fileStorageService = fileStorageService ?? throw new ArgumentNullException(nameof(fileStorageService));
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// 设置上下文 (每次请求前调用)
    /// </summary>
    public void SetContext(Guid conversationId, Guid userId)
    {
        _conversationId = conversationId;
        _userId = userId;
    }

    /// <summary>
    /// 生成图片
    /// </summary>
    [KernelFunction, Description("生成图片（独立任务）。当用户明确要求画图/生成图片时调用。图片会在后台生成完成后单独发送给用户。")]
    public async Task<string> DrawImageAsync(
        [Description("对要生成图片的详细描述，例如：一只可爱的橘猫在阳光下睡觉")] string prompt,
        [Description("图片宽度（像素），默认 1024。支持的值：256, 512, 1024, 1792")] int width = 1024,
        [Description("图片高度（像素），默认 1024。支持的值：256, 512, 1024, 1792")] int height = 1024)
    {
        _logger.LogDebug("[ImageGenerationPlugin] DrawImageAsync called | Prompt={Prompt} ConversationId={ConversationId}", prompt, _conversationId);

        if (_conversationId == Guid.Empty || _userId == Guid.Empty)
        {
            _logger.LogWarning("[ImageGenerationPlugin] Context not set. Cannot schedule job.");
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = "系统内部错误：上下文未初始化 (ConversationId/UserId missing)"
            });
        }

        // 提交后台任务
        _backgroundJobClient.Enqueue<Services.Jobs.ImageGenerationJob>(job =>
            job.ExecuteAsync(prompt, width, height, _conversationId, _userId));

        // 立即返回，通知 LLM 任务已接受
        // LLM 将会据此回复用户 "正在生成中"
        return JsonSerializer.Serialize(new
        {
            success = true,
            status = "processing",
            message = "画图任务已提交到后台，请稍候。图片生成大约需要 10-20 秒，完成后会自动发送给您。"
        });
    }

    /// <summary>
    /// 生成内联配图
    /// </summary>
    [KernelFunction, Description("生成图片并返回内联 Markdown 图片链接。当你认为回复中插入配图能更好表达时调用。默认 512x512，避免喧宾夺主。")]
    public async Task<string> GenerateInlineImageAsync(
        [Description("对要生成图片的详细描述，例如：一只可爱的橘猫在阳光下睡觉")] string prompt,
        [Description("图片宽度（像素），默认 512。支持的值：256, 512, 768, 1024, 1792")] int width = 512,
        [Description("图片高度（像素），默认 512。支持的值：256, 512, 768, 1024, 1792")] int height = 512)
    {
        _logger.LogDebug("[ImageGenerationPlugin] GenerateInlineImageAsync called | Prompt={Prompt}", prompt);

        var result = await _kernelService.GenerateImageAsync(
            prompt,
            width,
            height,
            auditScope: new ModelInvocationScopeDto
            {
                OwnerType = _userId == Guid.Empty ? ModelInvocationOwnerTypes.System : ModelInvocationOwnerTypes.User,
                OwnerUserId = _userId == Guid.Empty ? null : _userId,
                SessionId = _conversationId == Guid.Empty ? null : _conversationId,
                SceneCode = ModelInvocationSceneCodes.GenerationImageCreate,
                SceneCategory = ModelInvocationSceneCategories.UserFacing,
                ResourceType = _conversationId == Guid.Empty ? ModelInvocationResourceTypes.None : ModelInvocationResourceTypes.Session,
                ResourceId = _conversationId == Guid.Empty ? null : _conversationId.ToString()
            });
        if (!result.Success || string.IsNullOrEmpty(result.ImageUrl))
        {
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = result.Error ?? "图片生成失败，请稍后重试"
            });
        }

        string savedImageUrl;
        try
        {
            savedImageUrl = await DownloadAndSaveImageAsync(result.ImageUrl);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[ImageGenerationPlugin] Failed to download/save image, using original URL");
            savedImageUrl = result.ImageUrl;
        }

        return JsonSerializer.Serialize(new
        {
            success = true,
            imageUrl = savedImageUrl,
            markdown = $"![Generated Image]({savedImageUrl})"
        });
    }

    private async Task<string> DownloadAndSaveImageAsync(string imageUrl)
    {
        using var client = _httpClientFactory.CreateClient();
        var imageBytes = await client.GetByteArrayAsync(imageUrl);

        using var stream = new MemoryStream(imageBytes);
        var fileName = $"gen_{Guid.NewGuid()}.png";

        return await _fileStorageService.UploadFileAsync(stream, fileName, "image/png");
    }
}
