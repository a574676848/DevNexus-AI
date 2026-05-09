// using DevNexus.Domain.Abstractions via GlobalUsings
using DevNexus.Core.Extensions;
using DevNexus.Shared.DTOs;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace DevNexus.Core.Services.Chat;

public partial class ArtifactContextStrategy
{
    private string ExtractArtifactContent(ArtifactDto artifact, string? query, int remainingTokenBudget)
    {
        string content = artifact.Content ?? string.Empty;
        
        if (SmartDocumentExtensions.IsSmartDocumentArtifact(artifact.Type))
        {
            var smartDoc = SmartDocumentExtensions.TryParseFromJson(artifact.Content);
            if (smartDoc != null)
            {
                if (smartDoc.Content is TableDocumentContent tableContent)
                {
                    return BuildRelevantTableContext(tableContent, query, remainingTokenBudget);
                }

                content = smartDoc.ExtractTextContent(artifact.Content ?? string.Empty);
            }
        }

        var maxChars = Math.Max(800, remainingTokenBudget * 3);
        if (content.Length > maxChars)
        {
            content = content[..maxChars] + "\n\n... [内容已截断]";
        }

        return content;
    }

    /// <summary>
    /// 基于查询提取表格相关列/行，避免将大 CSV 全量注入上下文。
    /// </summary>
    private string BuildRelevantTableContext(TableDocumentContent table, string? query, int remainingTokenBudget)
    {
        var maxChars = Math.Max(1000, remainingTokenBudget * 3);
        var queryTerms = ExtractQueryTerms(query);

        var lines = (table.CsvRepresentation ?? string.Empty)
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.TrimEnd('\r'))
            .ToList();

        var headers = table.Headers
            .Where(header => !string.IsNullOrWhiteSpace(header))
            .Select(header => header.Trim())
            .ToList();

        if (headers.Count == 0 && lines.Count > 0)
        {
            headers = ParseCsvLine(lines[0])
                .Select(cell => cell.Trim())
                .Where(cell => !string.IsNullOrWhiteSpace(cell))
                .ToList();
        }

        if (headers.Count == 0)
        {
            var summaryOnly = table.Summary ?? $"表格共 {table.RowCount} 行 {table.ColumnCount} 列。";
            return summaryOnly.Length > maxChars ? summaryOnly[..maxChars] : summaryOnly;
        }

        var selectedHeaderIndexes = SelectRelevantColumns(headers, queryTerms, maxColumns: 6);
        var selectedHeaders = selectedHeaderIndexes.Select(index => headers[index]).ToList();

        var rowLines = lines.Count > 1 ? lines.Skip(1).Take(500).ToList() : new List<string>();
        var scoredRows = new List<(int Score, List<string> Cells)>();

        foreach (var rowLine in rowLines)
        {
            var cells = ParseCsvLine(rowLine);
            if (cells.Count == 0)
            {
                continue;
            }

            var score = ScoreRow(cells, selectedHeaderIndexes, queryTerms);
            scoredRows.Add((score, cells));
        }

        var chosenRows = queryTerms.Count > 0
            ? scoredRows
                .Where(item => item.Score > 0)
                .OrderByDescending(item => item.Score)
                .Take(20)
                .Select(item => item.Cells)
                .ToList()
            : new List<List<string>>();

        if (chosenRows.Count == 0)
        {
            chosenRows = scoredRows.Take(8).Select(item => item.Cells).ToList();
        }

        var builder = new StringBuilder();
        builder.AppendLine($"[表格摘要] {table.RowCount} 行 × {table.ColumnCount} 列");
        if (!string.IsNullOrWhiteSpace(table.Summary))
        {
            builder.AppendLine(table.Summary);
        }

        if (table.SheetNames?.Count > 0)
        {
            builder.AppendLine($"工作表: {string.Join(", ", table.SheetNames.Take(5))}");
        }

        if (queryTerms.Count > 0)
        {
            builder.AppendLine($"查询关键词: {string.Join(", ", queryTerms)}");
        }

        builder.AppendLine($"关注列: {string.Join(", ", selectedHeaders)}");
        builder.AppendLine();
        builder.AppendLine("| " + string.Join(" | ", selectedHeaders.Select(EscapeMarkdownCell)) + " |");
        builder.AppendLine("| " + string.Join(" | ", selectedHeaders.Select(_ => "---")) + " |");

        foreach (var cells in chosenRows)
        {
            var rowValues = selectedHeaderIndexes
                .Select(index => index < cells.Count ? EscapeMarkdownCell(cells[index]) : string.Empty)
                .ToList();
            builder.AppendLine("| " + string.Join(" | ", rowValues) + " |");

            if (builder.Length >= maxChars)
            {
                break;
            }
        }

        if (builder.Length > maxChars)
        {
            return builder.ToString()[..maxChars] + "\n\n... [表格上下文已截断]";
        }

        return builder.ToString();
    }

    private static List<int> SelectRelevantColumns(IReadOnlyList<string> headers, IReadOnlyList<string> queryTerms, int maxColumns)
    {
        if (headers.Count == 0)
        {
            return new List<int>();
        }

        if (queryTerms.Count == 0)
        {
            return Enumerable.Range(0, Math.Min(maxColumns, headers.Count)).ToList();
        }

        var ranked = headers
            .Select((header, index) => new
            {
                Index = index,
                Score = queryTerms.Count(term =>
                    header.Contains(term, StringComparison.OrdinalIgnoreCase))
            })
            .Where(item => item.Score > 0)
            .OrderByDescending(item => item.Score)
            .ThenBy(item => item.Index)
            .Take(maxColumns)
            .Select(item => item.Index)
            .ToList();

        if (ranked.Count == 0)
        {
            return Enumerable.Range(0, Math.Min(maxColumns, headers.Count)).ToList();
        }

        return ranked;
    }

    private static int ScoreRow(
        IReadOnlyList<string> cells,
        IReadOnlyList<int> selectedHeaderIndexes,
        IReadOnlyList<string> queryTerms)
    {
        if (queryTerms.Count == 0)
        {
            return 0;
        }

        int score = 0;
        foreach (var index in selectedHeaderIndexes)
        {
            var cellValue = index < cells.Count ? cells[index] : string.Empty;
            foreach (var term in queryTerms)
            {
                if (cellValue.Contains(term, StringComparison.OrdinalIgnoreCase))
                {
                    score++;
                }
            }
        }

        return score;
    }

    private static List<string> ExtractQueryTerms(string? query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return new List<string>();
        }

        return Regex.Matches(query, @"[\u4e00-\u9fff]+|[A-Za-z0-9_]+")
            .Select(match => match.Value.Trim())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Where(value => value.Any(ch => ch >= 0x4E00 && ch <= 0x9FFF) || value.Length >= 2)
            .Select(value => value.ToLowerInvariant())
            .Distinct()
            .Take(12)
            .ToList();
    }

    private static List<string> ParseCsvLine(string line)
    {
        var cells = new List<string>();
        var current = new StringBuilder();
        bool inQuotes = false;

        for (int i = 0; i < line.Length; i++)
        {
            var ch = line[i];
            if (ch == '"')
            {
                // 双引号转义
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                {
                    current.Append('"');
                    i++;
                    continue;
                }

                inQuotes = !inQuotes;
                continue;
            }

            if (ch == ',' && !inQuotes)
            {
                cells.Add(current.ToString());
                current.Clear();
                continue;
            }

            current.Append(ch);
        }

        cells.Add(current.ToString());
        return cells;
    }

    private static string EscapeMarkdownCell(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        var normalized = value.Replace("\r", " ").Replace("\n", " ").Replace("|", "\\|").Trim();
        if (normalized.Length > 120)
        {
            return normalized[..120] + "...";
        }

        return normalized;
    }

    /// <summary>
    /// 添加文档内容到上下文
    /// </summary>
    private void AppendDocumentContent(StringBuilder builder, string name, string type, string content)
    {
        builder.AppendLine($"### 📄 {name}");
        builder.AppendLine($"**类型**: {type}");
        builder.AppendLine();

        if (content.StartsWith("[表格摘要]", StringComparison.Ordinal))
        {
            builder.AppendLine(content);
        }
        else
        {
            builder.AppendLine("```" + GetCodeBlockLanguage(type));
            builder.AppendLine(content);
            builder.AppendLine("```");
        }

        builder.AppendLine();
    }

    private string GetCodeBlockLanguage(string type)
    {
        return type.ToLowerInvariant() switch
        {
            "markdown" or "text" => "",
            "csharp" or "c#" => "csharp",
            "javascript" or "js" => "javascript",
            "typescript" or "ts" => "typescript",
            "python" or "py" => "python",
            "json" => "json",
            "xml" => "xml",
            "html" => "html",
            "css" => "css",
            "sql" => "sql",
            "yaml" or "yml" => "yaml",
            _ => type.ToLowerInvariant()
        };
    }

    /// <summary>
    /// 估算 Token 数量
    /// </summary>
    private static int EstimateTokenCount(string text)
    {
        if (string.IsNullOrEmpty(text)) return 0;
        
        var chineseCount = text.Count(c => c >= 0x4E00 && c <= 0x9FFF);
        var otherCount = text.Length - chineseCount;
        
        return (int)Math.Ceiling(chineseCount / 1.5) + (int)Math.Ceiling(otherCount / 4.0);
    }
}
