using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Net.Http;
using System.Net.Http.Headers;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DevNexus.Infrastructure.Services.Parsing.PaddleOCR;

public class PaddleOcrClient : IPaddleOcrClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<PaddleOcrClient> _logger;
    private readonly PaddleOcrOptions _options;

    public PaddleOcrClient(
        HttpClient httpClient,
        IOptions<PaddleOcrOptions> options,
        ILogger<PaddleOcrClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _options = options.Value;
    }

    public async Task<string> RecognizeTextAsync(byte[] imageBytes, CancellationToken cancellationToken = default)
    {
        var items = await RecognizeStructureAsync(imageBytes, cancellationToken);
        var sb = new StringBuilder();
        foreach (var item in items)
        {
            sb.AppendLine(item.Text);
        }
        return sb.ToString();
    }

    public async Task<List<OcrResultItem>> RecognizeStructureAsync(byte[] imageBytes, CancellationToken cancellationToken = default)
    {
        try
        {
            if (imageBytes == null || imageBytes.Length == 0)
            {
                return new List<OcrResultItem>();
            }

            // 构建 multipart/form-data 请求，包含 file 字段
            using (var content = new MultipartFormDataContent())
            {
                var fileContent = new ByteArrayContent(imageBytes);
                fileContent.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
                content.Add(fileContent, "file", "image.jpg");

                var requestUri = "/predict/ocr_system"; 
                if (_httpClient.BaseAddress == null && !string.IsNullOrEmpty(_options.Endpoint))
                {
                    var baseUrl = _options.Endpoint.TrimEnd('/');
                    requestUri = $"{baseUrl}/predict/ocr_system";
                }

                var response = await _httpClient.PostAsync(requestUri, content, cancellationToken);
                var json = await response.Content.ReadAsStringAsync(cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("PaddleOCR 请求失败: {StatusCode}, Body: {Body}", response.StatusCode, json);
                    return new List<OcrResultItem>();
                }

                var paddleResponse = JsonSerializer.Deserialize<PaddleResponse>(json);

                if (paddleResponse == null || paddleResponse.Status != "000")
                {
                    _logger.LogWarning("PaddleOCR 返回错误状态: {Status}, Msg: {Msg}", paddleResponse?.Status, paddleResponse?.Msg);
                    return new List<OcrResultItem>();
                }

                var resultItems = new List<OcrResultItem>();
                
                // PaddleOCR 结果是三维数组: results[image_index][text_index]
                // 我们只发了一张图，所以取 results[0]
                if (paddleResponse.Results != null && paddleResponse.Results.Count > 0)
                {
                    var firstImageResult = paddleResponse.Results[0];
                    foreach (var item in firstImageResult)
                    {
                        resultItems.Add(new OcrResultItem
                        {
                            Text = item.Text,
                            Confidence = item.Confidence,
                            Box = item.TextRegion
                        });
                    }
                }

                return resultItems;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "PaddleOCR 识别失败");
            // 失败时返回空列表，由调用方决定是否重试或 fallback
            return new List<OcrResultItem>(); 
        }
    }

    public async Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // 构造 1x1 白色像素 GIF 二进制数据进行测试
            byte[] tinyImageBytes = Convert.FromBase64String("R0lGODlhAQABAIAAAAAAAP///yH5BAEAAAAALAAAAAABAAEAAAIBRAA7");
            
            using (var content = new MultipartFormDataContent())
            {
                var fileContent = new ByteArrayContent(tinyImageBytes);
                fileContent.Headers.ContentType = new MediaTypeHeaderValue("image/gif");
                content.Add(fileContent, "file", "test.gif");
                
                var requestUri = "/predict/ocr_system";
                if (_httpClient.BaseAddress == null && !string.IsNullOrEmpty(_options.Endpoint))
                {
                    var baseUrl = _options.Endpoint.TrimEnd('/');
                    requestUri = $"{baseUrl}/predict/ocr_system";
                }

                var response = await _httpClient.PostAsync(requestUri, content, cancellationToken);
                return response.IsSuccessStatusCode;
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "PaddleOCR 健康检查失败");
            return false;
        }
    }

    // 内部响应模型
    private class PaddleResponse
    {
        [JsonPropertyName("msg")]
        public string Msg { get; set; } = string.Empty;

        [JsonPropertyName("results")]
        public List<List<PaddleResultItem>> Results { get; set; } = new();

        [JsonPropertyName("status")]
        public string Status { get; set; } = string.Empty;
    }

    private class PaddleResultItem
    {
        [JsonPropertyName("confidence")]
        public float Confidence { get; set; }

        [JsonPropertyName("text")]
        public string Text { get; set; } = string.Empty;

        [JsonPropertyName("text_region")]
        public List<List<int>> TextRegion { get; set; } = new();
    }
}
