using System.Text.Json.Serialization;

namespace DevNexus.Shared.DTOs;

/// <summary>
/// 图表 DTO
/// </summary>
public class ChartDto
{
    /// <summary>
    /// 图表类型 (plotly, echarts 等)
    /// </summary>
    [JsonPropertyName("type")]
    public string Type { get; set; } = "plotly";

    /// <summary>
    /// 图表标题
    /// </summary>
    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// 图表数据 (Plotly data 数组)
    /// </summary>
    [JsonPropertyName("data")]
    public object Data { get; set; } = new object();

    /// <summary>
    /// 图表布局配置 (Plotly layout)
    /// </summary>
    [JsonPropertyName("layout")]
    public object Layout { get; set; } = new object();

    /// <summary>
    /// 图表配置选项 (Plotly config)
    /// </summary>
    [JsonPropertyName("config")]
    public object? Config { get; set; }
}
