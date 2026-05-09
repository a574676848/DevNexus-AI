namespace DevNexus.Infrastructure.Services.Parsing.PaddleOCR;

public class PaddleOcrOptions
{
    public const string SectionName = "PaddleOCR";
    
    /// <summary>
    /// PaddleOCR 服务地址 (例如 http://localhost:5433)
    /// </summary>
    public string Endpoint { get; set; } = string.Empty;

    /// <summary>
    /// 超时时间 (秒)
    /// </summary>
    public int TimeoutSeconds { get; set; } = 30;
}
