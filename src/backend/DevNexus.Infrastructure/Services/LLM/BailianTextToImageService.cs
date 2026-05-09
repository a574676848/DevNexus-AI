using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.TextToImage;
using System.Net.Http.Json;
using System.Text.Json.Nodes;

namespace DevNexus.Infrastructure.Services.LLM;

/// <summary>
/// 阿里云百炼 (DashScope) 文生图服务实现
/// - 同步模式：qwen-image 系列、z-image 系列（multimodal-generation API）
/// - 异步模式：wanx 系列（image-generation API，需轮询）
/// 所有模型均使用统一的 messages 格式
/// </summary>
#pragma warning disable SKEXP0001 // Type is for evaluation purposes only
public class BailianTextToImageService : ITextToImageService
{
    private readonly string _apiKey;
    private readonly string _modelId;
    private readonly HttpClient _httpClient;
    private readonly ILogger _logger;

    public IReadOnlyDictionary<string, object?> Attributes => new Dictionary<string, object?> { ["ModelId"] = _modelId };

    public BailianTextToImageService(
        string apiKey,
        string modelId,
        HttpClient httpClient,
        ILoggerFactory loggerFactory)
    {
        _apiKey = apiKey;
        _modelId = modelId;
        _httpClient = httpClient;
        _logger = loggerFactory.CreateLogger<BailianTextToImageService>();
    }

    /// <summary>
    /// 生成图片
    /// </summary>
    public async Task<string> GenerateImageAsync(
        string description,
        int width,
        int height,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("[BailianTextToImage] Generating image. Model: {ModelId}, Prompt: {Prompt}", _modelId, description);

        // 新版模型使用 multimodal-generation API（同步返回）
        // 包括：qwen-image 系列、z-image 系列
        bool isApi = _modelId.Contains("qwen-image", StringComparison.OrdinalIgnoreCase) ||
                        _modelId.Contains("z-image", StringComparison.OrdinalIgnoreCase);

        return await GenerateImageWithRetryAsync(description, width, height, isApi, cancellationToken);
    }

    /// <summary>
    /// SK 要求的实现：获取图片内容列表
    /// </summary>
    public async Task<IReadOnlyList<ImageContent>> GetImageContentsAsync(
        TextContent input,
        PromptExecutionSettings? executionSettings = null,
        Kernel? kernel = null,
        CancellationToken cancellationToken = default)
    {
        // 从 TextContent 提取 prompt
        var prompt = input.Text ?? throw new ArgumentException("生成图像必须提供提示词");

        // 从 executionSettings 中解析宽高参数，使用默认值 1024x1024
        int width = 1024;
        int height = 1024;

        if (executionSettings?.ExtensionData != null)
        {
            if (executionSettings.ExtensionData.TryGetValue("width", out var widthObj) && widthObj != null)
            {
                width = Convert.ToInt32(widthObj);
            }
            if (executionSettings.ExtensionData.TryGetValue("height", out var heightObj) && heightObj != null)
            {
                height = Convert.ToInt32(heightObj);
            }

            // 也支持从 size 参数解析（格式如 "1024*1024" 或 "1024x1024"）
            if (executionSettings.ExtensionData.TryGetValue("size", out var sizeObj) && sizeObj is string sizeStr)
            {
                var parts = sizeStr.Split(new[] { '*', 'x', 'X' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 2 && int.TryParse(parts[0], out var w) && int.TryParse(parts[1], out var h))
                {
                    width = w;
                    height = h;
                }
            }
        }

        _logger.LogDebug("[BailianTextToImage] Using image size: {Width}x{Height}", width, height);

        var imageUrl = await GenerateImageAsync(prompt, width, height, cancellationToken);

        return new List<ImageContent>
        {
            new ImageContent
            {
                Uri = new Uri(imageUrl),
            }
        };
    }

    private async Task<string> GenerateImageWithRetryAsync(
        string description,
        int width,
        int height,
        bool isApi,
        CancellationToken cancellationToken)
    {
        var delays = new[] { 2, 4, 8 };
        Exception? lastException = null;

        for (var attempt = 0; attempt <= delays.Length; attempt++)
        {
            try
            {
                return isApi
                    ? await GenerateImageWithApiAsync(description, width, height, cancellationToken)
                    : await GenerateImageWithPollingAsync(description, width, height, cancellationToken);
            }
            catch (Exception ex) when (IsRateQuotaError(ex.Message) && attempt < delays.Length)
            {
                lastException = ex;
                var delaySeconds = delays[attempt];

                _logger.LogWarning(
                    ex,
                    "[BailianTextToImage] Rate limited. Retry {Attempt}/{MaxAttempts} after {DelaySeconds}s",
                    attempt + 1,
                    delays.Length,
                    delaySeconds);

                await Task.Delay(TimeSpan.FromSeconds(delaySeconds), cancellationToken);
            }
        }

        throw lastException ?? new InvalidOperationException("Bailian API error: rate limit exceeded.");
    }

    private static bool IsRateQuotaError(string? errorMessage)
    {
        return !string.IsNullOrWhiteSpace(errorMessage)
            && errorMessage.Contains("Throttling.RateQuota", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 使用新版 multimodal-generation API 生成图片
    /// 支持模型：qwen-image-max/plus/ultra、z-image-turbo/ultra 等
    /// </summary>
    private async Task<string> GenerateImageWithApiAsync(string description, int width, int height, CancellationToken cancellationToken)
    {
        var requestUrl = "api/v1/services/aigc/multimodal-generation/generation";

        var payload = new
        {
            model = _modelId,
            input = BuildMessagesInput(description),
            parameters = new
            {
                size = $"{width}*{height}"
            }
        };

        var request = new HttpRequestMessage(HttpMethod.Post, requestUrl);
        request.Headers.Add("Authorization", $"Bearer {_apiKey}");
        request.Content = JsonContent.Create(payload);

        _logger.LogInformation("[BailianTextToImage] Calling new multimodal-generation API. Model: {Model}", _modelId);

        var response = await _httpClient.SendAsync(request, cancellationToken);
        var contentString = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("[BailianTextToImage] New API generation failed. Status: {StatusCode}, Content: {Content}", response.StatusCode, contentString);
            throw new InvalidOperationException($"Bailian API 错误：{contentString}");
        }

        return ExtractImageUrlFromResponse(contentString);
    }

    /// <summary>
    /// 异步生成与轮询（使用 image-generation API）
    /// 支持模型：wanx-v1、wan2.6-t2i 等万相系列模型
    /// </summary>
    private async Task<string> GenerateImageWithPollingAsync(string description, int width, int height, CancellationToken cancellationToken)
    {
        var requestUrl = "api/v1/services/aigc/image-generation/generation";

        var payload = new
        {
            model = _modelId,
            input = BuildMessagesInput(description),
            parameters = new
            {
                size = $"{width}*{height}",
                prompt_extend = true,
                watermark = false,
                n = 1
            }
        };

        var request = new HttpRequestMessage(HttpMethod.Post, requestUrl);
        request.Headers.Add("Authorization", $"Bearer {_apiKey}");
        request.Headers.Add("X-DashScope-Async", "enable"); // 强制异步作业
        request.Content = JsonContent.Create(payload);

        // 1. 提交异步任务
        var response = await _httpClient.SendAsync(request, cancellationToken);
        var contentString = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("[BailianTextToImage] Async submission failed. Status: {StatusCode}, Content: {Content}", response.StatusCode, contentString);
            throw new InvalidOperationException($"Bailian API 提交错误：{contentString}");
        }

        var jsonNode = JsonNode.Parse(contentString);
        var taskId = jsonNode?["output"]?["task_id"]?.GetValue<string>();

        if (string.IsNullOrEmpty(taskId))
        {
            throw new InvalidOperationException($"无法从响应中提取 task_id: {contentString}");
        }

        _logger.LogInformation("[BailianTextToImage] Task submitted successfully. TaskId: {TaskId}", taskId);

        // 2. 轮询任务状态
        return await PollTaskStatusAsync(taskId, cancellationToken);
    }

    /// <summary>
    /// 轮询任务状态
    /// </summary>
    private async Task<string> PollTaskStatusAsync(string taskId, CancellationToken cancellationToken)
    {
        var pollUrl = $"api/v1/tasks/{taskId}";
        // 轮询策略: 初始 2 秒，之后每次递增，最多等 60 秒
        int totalWaitSeconds = 0;
        int maxWaitSeconds = 60;
        int delaySeconds = 2;

        while (totalWaitSeconds < maxWaitSeconds)
        {
            await Task.Delay(TimeSpan.FromSeconds(delaySeconds), cancellationToken);
            totalWaitSeconds += delaySeconds;

            var request = new HttpRequestMessage(HttpMethod.Get, pollUrl);
            request.Headers.Add("Authorization", $"Bearer {_apiKey}");

            var response = await _httpClient.SendAsync(request, cancellationToken);
            var contentString = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("[BailianTextToImage] Task polling request failed. Status: {StatusCode}, Content: {Content}", response.StatusCode, contentString);
                continue; // 可能是临时网络问题，继续重试
            }

            var jsonNode = JsonNode.Parse(contentString);
            var taskStatus = jsonNode?["output"]?["task_status"]?.GetValue<string>();

            _logger.LogDebug("[BailianTextToImage] Task {TaskId} status: {Status}", taskId, taskStatus);

            if (string.Equals(taskStatus, "SUCCEEDED", StringComparison.OrdinalIgnoreCase))
            {
                // 获取图片 URL
                return ExtractImageUrlFromResponse(contentString);
            }
            else if (string.Equals(taskStatus, "FAILED", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(taskStatus, "UNKNOWN", StringComparison.OrdinalIgnoreCase))
            {
                var errorCode = jsonNode?["output"]?["code"]?.GetValue<string>();
                var errorMsg = jsonNode?["output"]?["message"]?.GetValue<string>();
                throw new InvalidOperationException($"Bailian 图像生成任务失败。错误码：{errorCode}, 错误信息：{errorMsg}");
            }
            else
            {
                // PENDING 或 RUNNING 状态，继续等待
            }
        }

        throw new TimeoutException($"Polling DashScope image generation task timed out after {maxWaitSeconds} seconds.");
    }

    /// <summary>
    /// 构建统一的 messages 格式输入
    /// </summary>
    private static object BuildMessagesInput(string description)
    {
        return new
        {
            messages = new[]
            {
                new
                {
                    role = "user",
                    content = new[]
                    {
                        new { text = description }
                    }
                }
            }
        };
    }

    /// <summary>
    /// 从 API 响应中提取图片 URL
    /// 通用格式：output.choices[0].message.content[0].image
    /// </summary>
    private string ExtractImageUrlFromResponse(string jsonContent)
    {
        var jsonNode = JsonNode.Parse(jsonContent);

        var choices = jsonNode?["output"]?["choices"] as JsonArray;
        if (choices != null && choices.Count > 0)
        {
            var message = choices[0]?["message"];
            var content = message?["content"] as JsonArray;
            if (content != null && content.Count > 0)
            {
                var imageUrl = content[0]?["image"]?.GetValue<string>();
                if (!string.IsNullOrEmpty(imageUrl))
                {
                    return imageUrl;
                }
            }
        }

        throw new InvalidOperationException($"API 响应格式异常：{jsonContent}");
    }
}
#pragma warning restore SKEXP0001
