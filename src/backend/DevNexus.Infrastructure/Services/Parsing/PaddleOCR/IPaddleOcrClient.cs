using DevNexus.Shared.DTOs;

namespace DevNexus.Infrastructure.Services.Parsing.PaddleOCR;

/// <summary>
/// PaddleOCR 客户端接口
/// </summary>
public interface IPaddleOcrClient
{
    /// <summary>
    /// 识别图片中的文字
    /// </summary>
    /// <param name="imageBytes">图片字节数组</param>
    /// <returns>识别结果文本 (换行分隔)</returns>
    Task<string> RecognizeTextAsync(byte[] imageBytes, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 识别图片中的文字 (返回详细结构，包含坐标和置信度)
    /// </summary>
    Task<List<OcrResultItem>> RecognizeStructureAsync(byte[] imageBytes, CancellationToken cancellationToken = default);

    /// <summary>
    /// 检查服务是否可用
    /// </summary>
    Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default);
}

public class OcrResultItem
{
    public string Text { get; set; } = string.Empty;
    public float Confidence { get; set; }
    // 简化处理，暂时存储为 Box 坐标列表 [[x1, y1], [x2, y2], [x3, y3], [x4, y4]]
    public List<List<int>> Box { get; set; } = new();
}
