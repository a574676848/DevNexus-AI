using DevNexus.Domain.Abstractions;
using DevNexus.Shared.DTOs;
using Mammoth;
using Microsoft.Extensions.Logging;
using Microsoft.KernelMemory;
using MiniExcelLibs;
using System.Text;
using System.Text.Json;
using UglyToad.PdfPig;

namespace DevNexus.Infrastructure.Services.Parsing;

/// <summary>
/// Kernel Memory 文档解析适配器（本地抽取 + 结构化 SmartDocument）。
/// </summary>
public class KernelMemoryDocumentParser : ISmartDocumentParser
{
    private const int TableChunkRowWindow = 200;
    private const int PreviewSheetCount = 3;
    private const int PreviewRowsPerSheet = 20;
    private const string UnknownUserId = "Unknown";
    private const string TableChunkingStrategyValue = "sheet-row-window";
    private const string ImportMethodValue = "LocalExtraction+KM_RAG";
    private const string ParserNameValue = "KernelMemoryDocumentParser";

    private readonly IKernelMemory _kernelMemory;
    private readonly ILogger<KernelMemoryDocumentParser> _logger;

    private static class DocumentMetadataKeys
    {
        public const string ImportMethod = "ImportMethod";
        public const string Parser = "Parser";
        public const string UserId = "UserId";
        public const string TableChunkingStrategy = "TableChunkingStrategy";
        public const string TableChunkRowWindow = "TableChunkRowWindow";
    }

    private sealed class StructuredTableParseResult
    {
        public TableDocumentContent Content { get; init; } = new();
        public List<SmartChunk> Chunks { get; init; } = new();
    }

    private sealed class TableSheetData
    {
        public string SheetName { get; init; } = string.Empty;
        public List<string> Headers { get; init; } = new();
        public List<Dictionary<string, string>> Rows { get; init; } = new();
    }

    public KernelMemoryDocumentParser(
        IKernelMemory kernelMemory,
        ILogger<KernelMemoryDocumentParser> logger)
    {
        _kernelMemory = kernelMemory;
        _logger = logger;
    }

    public bool CanParse(string mimeType)
    {
        return mimeType switch
        {
            "application/pdf" => true,
            "application/msword" => true,
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document" => true,
            "application/vnd.ms-excel" => true,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" => true,
            _ => mimeType.StartsWith("text/", StringComparison.OrdinalIgnoreCase)
        };
    }

    public async Task<SmartDocument> ParseAsync(
        Stream fileStream,
        string fileName,
        string? mimeType = null,
        DevNexus.Domain.Abstractions.ParsingOptions? options = null,
        ParsingContext? context = null)
    {
        _ = _kernelMemory;

        var docId = Guid.NewGuid().ToString();
        var userId = context?.UserId;
        var effectiveMimeType = mimeType ?? SmartDocumentParserFactory.GetMimeType(fileName);

        _logger.LogInformation("Parsing document: {FileName} ({MimeType})", fileName, effectiveMimeType);

        string extractedText = string.Empty;
        int pageCount = 0;
        bool hasImages = false;
        StructuredTableParseResult? tableParseResult = null;

        using var workingStream = new MemoryStream();
        await fileStream.CopyToAsync(workingStream, context?.CancellationToken ?? CancellationToken.None);
        workingStream.Position = 0;

        try
        {
            switch (effectiveMimeType)
            {
                case "application/pdf":
                    (extractedText, pageCount) = ExtractTextFromPdf(workingStream);
                    break;
                case "application/vnd.openxmlformats-officedocument.wordprocessingml.document":
                case "application/msword":
                    extractedText = ExtractTextFromDocx(workingStream);
                    break;
                case "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet":
                case "application/vnd.ms-excel":
                    tableParseResult = ExtractTableFromExcel(workingStream, fileName);
                    extractedText = tableParseResult.Content.CsvRepresentation;
                    break;
                default:
                    if (IsCsvDocument(effectiveMimeType, fileName))
                    {
                        tableParseResult = ExtractTableFromCsv(workingStream, fileName);
                        extractedText = tableParseResult.Content.CsvRepresentation;
                    }
                    else if (effectiveMimeType.StartsWith("text/", StringComparison.OrdinalIgnoreCase))
                    {
                        extractedText = await ExtractTextFromStreamAsync(workingStream);
                    }
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to extract text from document: {FileName}", fileName);
        }

        var tableSucceeded = tableParseResult?.Chunks.Count > 0 || (tableParseResult?.Content.ColumnCount ?? 0) > 0;
        var status = tableSucceeded || !string.IsNullOrWhiteSpace(extractedText)
            ? ParsingStatus.Completed
            : ParsingStatus.Failed;

        var doc = new SmartDocument
        {
            FileId = docId,
            FileName = fileName,
            MimeType = effectiveMimeType,
            Status = status,
            Metadata = new Dictionary<string, object>
            {
                [DocumentMetadataKeys.ImportMethod] = ImportMethodValue,
                [DocumentMetadataKeys.Parser] = ParserNameValue,
                [DocumentMetadataKeys.UserId] = userId ?? UnknownUserId
            }
        };

        if (status == ParsingStatus.Completed)
        {
            if (tableParseResult != null)
            {
                doc.Content = tableParseResult.Content;
                doc.Chunks = tableParseResult.Chunks;
                doc.Metadata[DocumentMetadataKeys.TableChunkingStrategy] = TableChunkingStrategyValue;
                doc.Metadata[DocumentMetadataKeys.TableChunkRowWindow] = TableChunkRowWindow;
            }
            else
            {
                doc.Content = new TextDocumentContent
                {
                    Text = extractedText,
                    PageCount = pageCount > 0 ? pageCount : 1,
                    Format = effectiveMimeType.Contains("markdown", StringComparison.OrdinalIgnoreCase) ? "markdown" : "plain",
                    HasImages = hasImages
                };
            }

            _logger.LogInformation(
                "Successfully extracted {Length} chars from {FileName}. Created structured document content.",
                extractedText.Length,
                fileName);
        }

        return doc;
    }

    private (string Text, int PageCount) ExtractTextFromPdf(Stream stream)
    {
        var sb = new StringBuilder();
        int pages = 0;

        try
        {
            using var document = PdfDocument.Open(stream);
            pages = document.NumberOfPages;
            foreach (var page in document.GetPages())
            {
                if (!string.IsNullOrWhiteSpace(page.Text))
                {
                    sb.AppendLine(page.Text);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "PdfPig failed to extract text");
        }

        return (sb.ToString(), pages);
    }

    private string ExtractTextFromDocx(Stream stream)
    {
        try
        {
            var converter = new DocumentConverter();
            var result = converter.ExtractRawText(stream);
            return result.Value ?? string.Empty;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Mammoth failed to extract text");
            return string.Empty;
        }
    }

    private StructuredTableParseResult ExtractTableFromExcel(Stream stream, string fileName)
    {
        var sheets = new List<TableSheetData>();

        try
        {
            stream.Position = 0;
            var sheetNames = MiniExcel.GetSheetNames(stream).ToList();
            foreach (var sheetName in sheetNames)
            {
                stream.Position = 0;
                var rows = MiniExcel.Query(stream, sheetName: sheetName);
                var sheetData = new TableSheetData
                {
                    SheetName = sheetName
                };

                foreach (var row in rows)
                {
                    if (row is not IDictionary<string, object> dict)
                    {
                        continue;
                    }

                    if (sheetData.Headers.Count == 0)
                    {
                        sheetData.Headers.AddRange(dict.Keys.Select(NormalizeHeader));
                    }

                    var normalizedRow = CreateNormalizedRow(dict, sheetData.Headers);
                    if (normalizedRow.Count > 0)
                    {
                        sheetData.Rows.Add(normalizedRow);
                    }
                }

                if (sheetData.Headers.Count > 0)
                {
                    sheets.Add(sheetData);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "MiniExcel failed to extract table from Excel: {FileName}", fileName);
        }

        return BuildTableParseResult(fileName, sheets);
    }

    private StructuredTableParseResult ExtractTableFromCsv(Stream stream, string fileName)
    {
        var sheets = new List<TableSheetData>();

        try
        {
            stream.Position = 0;
            using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
            var content = reader.ReadToEnd();
            var lines = content
                .Split('\n', StringSplitOptions.RemoveEmptyEntries)
                .Select(line => line.TrimEnd('\r'))
                .ToList();

            if (lines.Count == 0)
            {
                return BuildTableParseResult(fileName, sheets);
            }

            var headers = ParseCsvLine(lines[0]).Select(NormalizeHeader).ToList();
            var rows = new List<Dictionary<string, string>>();
            for (int i = 1; i < lines.Count; i++)
            {
                var values = ParseCsvLine(lines[i]);
                var row = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                for (int col = 0; col < headers.Count; col++)
                {
                    row[headers[col]] = col < values.Count ? values[col] : string.Empty;
                }

                if (row.Count > 0)
                {
                    rows.Add(row);
                }
            }

            if (headers.Count > 0)
            {
                sheets.Add(new TableSheetData
                {
                    SheetName = "CSV",
                    Headers = headers,
                    Rows = rows
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to extract table from CSV: {FileName}", fileName);
        }

        return BuildTableParseResult(fileName, sheets);
    }

    private StructuredTableParseResult BuildTableParseResult(string fileName, IReadOnlyList<TableSheetData> sheets)
    {
        var sheetNames = sheets.Select(sheet => sheet.SheetName).ToList();
        var primaryHeaders = sheets.FirstOrDefault(sheet => sheet.Headers.Count > 0)?.Headers ?? new List<string>();
        var totalRowCount = sheets.Sum(sheet => sheet.Rows.Count);
        var columnCount = sheets.Count == 0 ? 0 : sheets.Max(sheet => sheet.Headers.Count);

        return new StructuredTableParseResult
        {
            Content = new TableDocumentContent
            {
                CsvRepresentation = BuildTablePreviewCsv(sheets),
                Headers = primaryHeaders,
                RowCount = totalRowCount,
                ColumnCount = columnCount,
                Summary = BuildTableSummary(fileName, totalRowCount, columnCount, sheetNames),
                SheetNames = sheetNames.Count > 0 ? sheetNames : null
            },
            Chunks = BuildTableChunks(sheets)
        };
    }

    private static Dictionary<string, string> CreateNormalizedRow(
        IDictionary<string, object> rawRow,
        IReadOnlyList<string> headers)
    {
        var row = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var headerIndex = 0;
        foreach (var pair in rawRow)
        {
            var header = headerIndex < headers.Count
                ? headers[headerIndex]
                : NormalizeHeader(pair.Key);
            row[header] = pair.Value?.ToString() ?? string.Empty;
            headerIndex++;
        }

        return row;
    }

    private static List<SmartChunk> BuildTableChunks(IReadOnlyList<TableSheetData> sheets)
    {
        var chunks = new List<SmartChunk>();

        foreach (var sheet in sheets)
        {
            if (sheet.Headers.Count == 0 || sheet.Rows.Count == 0)
            {
                continue;
            }

            for (int offset = 0; offset < sheet.Rows.Count; offset += TableChunkRowWindow)
            {
                var windowRows = sheet.Rows.Skip(offset).Take(TableChunkRowWindow).ToList();
                var rowStart = offset + 1;
                var rowEnd = offset + windowRows.Count;
                var headersJson = JsonSerializer.Serialize(sheet.Headers);
                var structuredData = JsonSerializer.Serialize(new
                {
                    sheetName = sheet.SheetName,
                    rowStart,
                    rowEnd,
                    headers = sheet.Headers,
                    rowCount = windowRows.Count,
                    columnCount = sheet.Headers.Count
                });

                chunks.Add(new SmartChunk
                {
                    Id = Guid.NewGuid().ToString(),
                    Type = ChunkType.Table,
                    Content = BuildCsvFromRows(sheet.Headers, windowRows, windowRows.Count),
                    StructuredData = structuredData,
                    Metadata = new Dictionary<string, object>
                    {
                        [TableChunkMetadataKeys.SheetName] = sheet.SheetName,
                        [TableChunkMetadataKeys.RowStart] = rowStart,
                        [TableChunkMetadataKeys.RowEnd] = rowEnd,
                        [TableChunkMetadataKeys.RowCount] = windowRows.Count,
                        [TableChunkMetadataKeys.ColumnCount] = sheet.Headers.Count,
                        [TableChunkMetadataKeys.HeadersJson] = headersJson,
                        [TableChunkMetadataKeys.HeadersText] = string.Join(" | ", sheet.Headers),
                        [TableChunkMetadataKeys.ChunkLabel] = $"{sheet.SheetName} [{rowStart}-{rowEnd}]"
                    }
                });
            }
        }

        return chunks;
    }

    private static string BuildTablePreviewCsv(IReadOnlyList<TableSheetData> sheets)
    {
        if (sheets.Count == 0)
        {
            return string.Empty;
        }

        var builder = new StringBuilder();
        foreach (var sheet in sheets.Take(PreviewSheetCount))
        {
            if (sheet.Headers.Count == 0)
            {
                continue;
            }

            builder.AppendLine($"[Sheet: {sheet.SheetName}]");
            builder.AppendLine(string.Join(",", sheet.Headers.Select(EscapeCsvCell)));
            foreach (var row in sheet.Rows.Take(PreviewRowsPerSheet))
            {
                builder.AppendLine(string.Join(",", sheet.Headers.Select(header =>
                {
                    row.TryGetValue(header, out var value);
                    return EscapeCsvCell(value ?? string.Empty);
                })));
            }

            if (sheet.Rows.Count > PreviewRowsPerSheet)
            {
                builder.AppendLine($"... ({sheet.Rows.Count - PreviewRowsPerSheet} more rows)");
            }

            builder.AppendLine();
        }

        return builder.ToString();
    }

    private async Task<string> ExtractTextFromStreamAsync(Stream stream)
    {
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
        return await reader.ReadToEndAsync();
    }

    private static bool IsCsvDocument(string mimeType, string fileName)
    {
        if (mimeType.Equals("text/csv", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return Path.GetExtension(fileName).Equals(".csv", StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeHeader(string? header)
    {
        if (string.IsNullOrWhiteSpace(header))
        {
            return "Column";
        }

        return header.Trim().Replace("\r", string.Empty).Replace("\n", " ");
    }

    private static string BuildCsvFromRows(
        IReadOnlyList<string> headers,
        IReadOnlyList<Dictionary<string, string>> rows,
        int maxRows)
    {
        if (headers.Count == 0)
        {
            return string.Empty;
        }

        var builder = new StringBuilder();
        builder.AppendLine(string.Join(",", headers.Select(EscapeCsvCell)));
        foreach (var row in rows.Take(maxRows))
        {
            builder.AppendLine(string.Join(",", headers.Select(header =>
            {
                row.TryGetValue(header, out var value);
                return EscapeCsvCell(value ?? string.Empty);
            })));
        }

        return builder.ToString();
    }

    private static string EscapeCsvCell(string value)
    {
        if (value.Contains('"'))
        {
            value = value.Replace("\"", "\"\"");
        }

        return value.Contains(',') || value.Contains('\n') || value.Contains('\r')
            ? $"\"{value}\""
            : value;
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

    private static string BuildTableSummary(
        string fileName,
        int rowCount,
        int columnCount,
        IReadOnlyList<string>? sheetNames)
    {
        var summary = $"{fileName} 包含 {rowCount} 行数据，{columnCount} 列。";
        if (sheetNames != null && sheetNames.Count > 0)
        {
            summary += $" 工作表 {sheetNames.Count} 个：{string.Join(", ", sheetNames.Take(5))}。";
        }

        summary += $" 已按每 {TableChunkRowWindow} 行进行分块索引。";
        return summary;
    }
}
