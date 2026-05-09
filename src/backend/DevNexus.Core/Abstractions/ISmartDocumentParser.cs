using DevNexus.Shared.DTOs;

namespace DevNexus.Core.Abstractions;

/// <summary>
/// 智能文档解析器接口
/// 负责将各种格式的文件解析为统一的 SmartDocument 结构
/// </summary>
public interface ISmartDocumentParser
{
    /// <summary>
    /// 解析文件流
    /// </summary>
    /// <param name="fileStream">文件内容流</param>
    /// <param name="fileName">原始文件名</param>
    /// <param name="mimeType">MIME 类型</param>
    /// <param name="options">解析选项</param>
    /// <returns>解析后的 SmartDocument</returns>
    Task<SmartDocument> ParseAsync(
        Stream fileStream, 
        string fileName, 
        string? mimeType = null,
        ParsingOptions? options = null,
        ParsingContext? context = null);

    /// <summary>
    /// 检查是否支持指定的 MIME 类型
    /// </summary>
    bool CanParse(string mimeType);
}
