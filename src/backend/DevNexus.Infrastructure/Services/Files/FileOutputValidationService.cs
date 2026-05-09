using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using DevNexus.Domain.Abstractions;
using DevNexus.Domain.Models;

namespace DevNexus.Infrastructure.Services.Files;

/// <summary>
/// 文件任务输出验证服务
/// </summary>
public class FileOutputValidationService : IFileOutputValidationService
{
    /// <inheritdoc />
    public async Task<FileOutputValidationResult> ValidateAsync(
        IReadOnlyCollection<string> generatedFiles,
        CancellationToken cancellationToken = default)
    {
        var items = new List<FileOutputValidationItem>();

        foreach (var generatedFile in generatedFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();
            items.Add(await ValidateSingleFileAsync(generatedFile, cancellationToken));
        }

        var failedItems = items.Where(item => !item.IsValid).ToList();
        return new FileOutputValidationResult
        {
            IsValid = failedItems.Count == 0,
            Summary = failedItems.Count == 0
                ? $"已验证 {items.Count} 个输出文件，全部通过。"
                : "验证失败: " + string.Join("; ", failedItems.Select(item => $"{Path.GetFileName(item.FilePath)} - {item.Summary}")),
            Items = items
        };
    }

    private static async Task<FileOutputValidationItem> ValidateSingleFileAsync(
        string filePath,
        CancellationToken cancellationToken)
    {
        if (IsReferenceOutput(filePath))
        {
            return Valid(filePath, "reference", "引用文件已保留，跳过结构校验");
        }

        var extension = Path.GetExtension(filePath).ToLowerInvariant();

        return extension switch
        {
            ".xlsx" => await ValidateXlsxAsync(filePath, cancellationToken),
            ".xls" => ValidateBinaryExcel(filePath),
            ".csv" => await ValidateCsvAsync(filePath, cancellationToken),
            ".md" or ".markdown" or ".txt" => await ValidateMarkdownAsync(filePath, cancellationToken),
            ".json" => await ValidateJsonAsync(filePath, cancellationToken),
            ".xml" => await ValidateXmlAsync(filePath, cancellationToken),
            ".cs" or ".csproj" or ".js" or ".ts" or ".tsx" or ".jsx" or ".py" or ".java" or ".go" or ".rs" or ".sql" or ".html" or ".css" or ".scss" or ".yml" or ".yaml" => await ValidateCodeAsync(filePath, cancellationToken),
            _ => ValidateGeneric(filePath)
        };
    }

    private static bool IsReferenceOutput(string filePath)
    {
        var normalized = filePath.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
        var marker = $"{Path.DirectorySeparatorChar}references{Path.DirectorySeparatorChar}";
        return normalized.Contains(marker, StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<FileOutputValidationItem> ValidateXlsxAsync(
        string filePath,
        CancellationToken cancellationToken)
    {
        try
        {
            using var archive = ZipFile.OpenRead(filePath);
            var workbookEntry = archive.Entries.FirstOrDefault(entry => entry.FullName == "xl/workbook.xml");
            var worksheetCount = archive.Entries.Count(entry => entry.FullName.StartsWith("xl/worksheets/", StringComparison.OrdinalIgnoreCase));

            if (workbookEntry == null || worksheetCount == 0)
            {
                return Invalid(filePath, "excel", "缺少工作簿或工作表结构");
            }

            await using var stream = workbookEntry.Open();
            using var reader = new StreamReader(stream, Encoding.UTF8, true);
            var content = await reader.ReadToEndAsync(cancellationToken);
            var xml = XDocument.Parse(content);
            var sheetCount = xml.Descendants().Count(node => node.Name.LocalName == "sheet");
            var worksheetEntries = archive.Entries
                .Where(entry => entry.FullName.StartsWith("xl/worksheets/", StringComparison.OrdinalIgnoreCase))
                .ToList();

            var nonEmptyCellCount = 0;
            var inspectedSheetCount = 0;
            foreach (var worksheetEntry in worksheetEntries)
            {
                await using var worksheetStream = worksheetEntry.Open();
                using var worksheetReader = new StreamReader(worksheetStream, Encoding.UTF8, true);
                var worksheetContent = await worksheetReader.ReadToEndAsync(cancellationToken);
                var worksheetXml = XDocument.Parse(worksheetContent);
                var cells = worksheetXml.Descendants().Where(node => node.Name.LocalName == "c").ToList();
                var nonEmptyCells = cells.Count(cell =>
                    cell.Descendants().Any(node => node.Name.LocalName is "v" or "t" && !string.IsNullOrWhiteSpace(node.Value)));

                if (nonEmptyCells > 0)
                {
                    inspectedSheetCount++;
                    nonEmptyCellCount += nonEmptyCells;
                }
            }

            if (nonEmptyCellCount == 0)
            {
                return Invalid(filePath, "excel", "工作簿结构存在，但未检测到有效单元格内容");
            }

            return Valid(
                filePath,
                "excel",
                $"检测到 {Math.Max(sheetCount, worksheetCount)} 个工作表，{inspectedSheetCount} 个工作表含有效内容，累计 {nonEmptyCellCount} 个非空单元格");
        }
        catch (Exception ex)
        {
            return Invalid(filePath, "excel", $"无法解析 xlsx 工作簿: {ex.Message}");
        }
    }

    private static FileOutputValidationItem ValidateBinaryExcel(string filePath)
    {
        var fileInfo = new FileInfo(filePath);
        return fileInfo.Exists && fileInfo.Length > 0
            ? Valid(filePath, "excel", $"二进制 Excel 文件大小 {fileInfo.Length} 字节")
            : Invalid(filePath, "excel", "xls 文件为空或不存在");
    }

    private static async Task<FileOutputValidationItem> ValidateCsvAsync(
        string filePath,
        CancellationToken cancellationToken)
    {
        try
        {
            var lines = await File.ReadAllLinesAsync(filePath, cancellationToken);
            var effectiveLines = lines.Where(line => !string.IsNullOrWhiteSpace(line)).ToList();
            if (effectiveLines.Count == 0)
            {
                return Invalid(filePath, "csv", "CSV 内容为空");
            }

            var headerColumns = effectiveLines[0].Split(',').Length;
            return headerColumns > 0
                ? Valid(filePath, "csv", $"检测到 {effectiveLines.Count - 1} 行数据，{headerColumns} 列")
                : Invalid(filePath, "csv", "CSV 缺少有效表头");
        }
        catch (Exception ex)
        {
            return Invalid(filePath, "csv", $"CSV 验证失败: {ex.Message}");
        }
    }

    private static async Task<FileOutputValidationItem> ValidateMarkdownAsync(
        string filePath,
        CancellationToken cancellationToken)
    {
        var text = await File.ReadAllTextAsync(filePath, cancellationToken);
        if (string.IsNullOrWhiteSpace(text))
        {
            return Invalid(filePath, "markdown", "文本内容为空");
        }

        var headingCount = text.Split('\n').Count(line => line.TrimStart().StartsWith('#'));
        return Valid(filePath, "markdown", $"文本非空，包含 {headingCount} 个标题行");
    }

    private static async Task<FileOutputValidationItem> ValidateJsonAsync(
        string filePath,
        CancellationToken cancellationToken)
    {
        try
        {
            var content = await File.ReadAllTextAsync(filePath, cancellationToken);
            JsonDocument.Parse(content);
            return Valid(filePath, "json", "JSON 结构有效");
        }
        catch (Exception ex)
        {
            return Invalid(filePath, "json", $"JSON 无法解析: {ex.Message}");
        }
    }

    private static async Task<FileOutputValidationItem> ValidateXmlAsync(
        string filePath,
        CancellationToken cancellationToken)
    {
        try
        {
            var content = await File.ReadAllTextAsync(filePath, cancellationToken);
            XDocument.Parse(content);
            return Valid(filePath, "xml", "XML 结构有效");
        }
        catch (Exception ex)
        {
            return Invalid(filePath, "xml", $"XML 无法解析: {ex.Message}");
        }
    }

    private static async Task<FileOutputValidationItem> ValidateCodeAsync(
        string filePath,
        CancellationToken cancellationToken)
    {
        try
        {
            var text = await File.ReadAllTextAsync(filePath, cancellationToken);
            if (string.IsNullOrWhiteSpace(text))
            {
                return Invalid(filePath, "code", "代码文件为空");
            }

            var lineCount = text.Split('\n').Length;
            return Valid(filePath, "code", $"代码文件可读取，共 {lineCount} 行");
        }
        catch (Exception ex)
        {
            return Invalid(filePath, "code", $"代码文件无法读取: {ex.Message}");
        }
    }

    private static FileOutputValidationItem ValidateGeneric(string filePath)
    {
        var fileInfo = new FileInfo(filePath);
        return fileInfo.Exists && fileInfo.Length > 0
            ? Valid(filePath, "generic", $"文件存在，大小 {fileInfo.Length} 字节")
            : Invalid(filePath, "generic", "文件不存在或为空");
    }

    private static FileOutputValidationItem Valid(string filePath, string category, string summary)
    {
        return new FileOutputValidationItem
        {
            FilePath = filePath,
            Category = category,
            IsValid = true,
            Summary = summary
        };
    }

    private static FileOutputValidationItem Invalid(string filePath, string category, string summary)
    {
        return new FileOutputValidationItem
        {
            FilePath = filePath,
            Category = category,
            IsValid = false,
            Summary = summary
        };
    }
}
