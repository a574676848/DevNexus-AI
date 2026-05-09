namespace DevNexus.Client.Shared.Models;

/// <summary>
/// 拖放文件数据传输对象 (从 JavaScript 传递到 Blazor)
/// </summary>
public class FileDropDto
{
    /// <summary>
    /// 文件名
    /// </summary>
    public string Name { get; set; } = "";
    
    /// <summary>
    /// 文件 MIME 类型
    /// </summary>
    public string Type { get; set; } = "";
    
    /// <summary>
    /// 文件大小（字节）
    /// </summary>
    public long Size { get; set; }
    
    /// <summary>
    /// 文件内容（字节数组）
    /// </summary>
    public byte[] Data { get; set; } = Array.Empty<byte>();
}

/// <summary>
/// 粘贴图片数据传输对象 (从 JavaScript 传递到 Blazor)
/// </summary>
public class PastedImageDto
{
    /// <summary>
    /// 图片文件名（自动生成）
    /// </summary>
    public string Name { get; set; } = "";
    
    /// <summary>
    /// 图片 MIME 类型
    /// </summary>
    public string Type { get; set; } = "";
    
    /// <summary>
    /// 图片大小（字节）
    /// </summary>
    public long Size { get; set; }
    
    /// <summary>
    /// 图片内容（Base64 编码字符串）
    /// </summary>
    public string Base64Data { get; set; } = "";
}
