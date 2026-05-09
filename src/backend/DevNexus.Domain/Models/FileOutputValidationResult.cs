namespace DevNexus.Domain.Models;

/// <summary>
/// 文件输出验证结果
/// </summary>
public class FileOutputValidationResult
{
    /// <summary>
    /// 是否全部通过验证
    /// </summary>
    public bool IsValid { get; set; }

    /// <summary>
    /// 验证摘要
    /// </summary>
    public string Summary { get; set; } = string.Empty;

    /// <summary>
    /// 单文件验证结果列表
    /// </summary>
    public List<FileOutputValidationItem> Items { get; set; } = new();
}

/// <summary>
/// 单个输出文件的验证结果
/// </summary>
public class FileOutputValidationItem
{
    /// <summary>
    /// 文件路径
    /// </summary>
    public string FilePath { get; set; } = string.Empty;

    /// <summary>
    /// 验证类别
    /// </summary>
    public string Category { get; set; } = string.Empty;

    /// <summary>
    /// 是否通过验证
    /// </summary>
    public bool IsValid { get; set; }

    /// <summary>
    /// 验证说明
    /// </summary>
    public string Summary { get; set; } = string.Empty;
}