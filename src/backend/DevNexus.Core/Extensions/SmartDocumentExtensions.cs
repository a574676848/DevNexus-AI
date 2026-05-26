using DevNexus.Shared.DTOs;

namespace DevNexus.Core.Extensions;

/// <summary>
/// SmartDocument 扩展方法
/// 统一后端对 SmartDocument 的解析和内容提取逻辑
/// </summary>
public static class SmartDocumentExtensions
{
    /// <summary>
    /// 从 SmartDocument 提取纯文本内容（用于 LLM 上下文注入）
    /// </summary>
    public static string ExtractTextContent(this SmartDocument? smartDoc, string fallback = "")
    {
        if (smartDoc == null)
        {
            return fallback;
        }

        var extracted = smartDoc.Content switch
        {
            TextDocumentContent text => text.Text,
            CodeDocumentContent code => code.Text,
            ImageDocumentContent img => img.Description,
            TableDocumentContent table => table.CsvRepresentation ?? $"[表格: {table.RowCount}行 x {table.ColumnCount}列]",
            null => null,
            _ => null
        };

        if (!string.IsNullOrWhiteSpace(extracted))
        {
            return extracted;
        }

        var chunkText = ExtractChunkText(smartDoc);
        if (!string.IsNullOrWhiteSpace(chunkText))
        {
            return chunkText;
        }

        if (smartDoc.Content is ImageDocumentContent)
        {
            return $"[图片: {smartDoc.FileName}]";
        }

        return fallback;
    }

    private static string? ExtractChunkText(SmartDocument smartDoc)
    {
        var chunks = smartDoc.Chunks
            .Where(chunk => !string.IsNullOrWhiteSpace(chunk.Content))
            .Select(chunk => chunk.Content.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToList();

        return chunks.Count == 0 ? null : string.Join("\n\n", chunks);
    }

    /// <summary>
    /// 获取显示类型（用于 LLM 上下文中的代码块语言标识）
    /// </summary>
    public static string GetDisplayType(this SmartDocument? smartDoc, string fallback = "text")
    {
        if (smartDoc?.Content == null) return fallback;

        return smartDoc.Content switch
        {
            CodeDocumentContent code => code.Language ?? "code",
            ImageDocumentContent => "image",
            TableDocumentContent => "csv",
            _ => smartDoc.MimeType ?? fallback
        };
    }

    /// <summary>
    /// 获取内容描述（用于日志和调试）
    /// </summary>
    public static string GetContentSummary(this SmartDocument? smartDoc)
    {
        if (smartDoc?.Content == null) return "[空文档]";

        return smartDoc.Content switch
        {
            TextDocumentContent text => $"文本 {text.PageCount}页 {text.Text?.Length ?? 0}字符",
            CodeDocumentContent code => $"{code.Language} {code.Metrics?.TotalLines ?? 0}行",
            ImageDocumentContent img => $"图片 {img.Width}x{img.Height}",
            TableDocumentContent table => $"表格 {table.RowCount}行 x {table.ColumnCount}列",
            _ => "[未知类型]"
        };
    }

    /// <summary>
    /// 尝试从 JSON 字符串解析 SmartDocument
    /// </summary>
    public static SmartDocument? TryParseFromJson(string? json)
    {
        if (string.IsNullOrEmpty(json)) return null;

        try
        {
            return System.Text.Json.JsonSerializer.Deserialize<SmartDocument>(json);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// 判断 Artifact 是否为 SmartDocument 类型
    /// 现在所有 Artifact 都统一为 SmartDocument 格式
    /// </summary>
    public static bool IsSmartDocumentArtifact(string? artifactType, string? content = null)
    {
        // 所有新 Artifact 都是 SmartDocument 格式，直接返回 true
        return !string.IsNullOrEmpty(content);
    }
}
