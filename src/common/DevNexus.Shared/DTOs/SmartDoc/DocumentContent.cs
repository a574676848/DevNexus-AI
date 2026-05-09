using System.Text.Json.Serialization;

namespace DevNexus.Shared.DTOs;

/// <summary>
/// 文档内容基类 - 使用 JsonDerivedType 支持多态序列化
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "contentType")]
[JsonDerivedType(typeof(TextDocumentContent), "text")]
[JsonDerivedType(typeof(TableDocumentContent), "table")]
[JsonDerivedType(typeof(ImageDocumentContent), "image")]
[JsonDerivedType(typeof(CodeDocumentContent), "code")]
public abstract class DocumentContent
{
    /// <summary>
    /// 内容类型标识
    /// </summary>
    [JsonIgnore]
    public abstract string ContentType { get; }
}

/// <summary>
/// 纯文本/富文本内容 (Word, MD, TXT, PDF转文本)
/// </summary>
public class TextDocumentContent : DocumentContent
{
    [JsonIgnore]
    public override string ContentType => "text";

    /// <summary>
    /// 清洗后的文本内容
    /// </summary>
    [JsonPropertyName("text")]
    public string Text { get; set; } = string.Empty;

    /// <summary>
    /// 文本格式: "markdown", "plain", "html"
    /// </summary>
    [JsonPropertyName("format")]
    public string Format { get; set; } = "plain";

    /// <summary>
    /// 文档结构 (标题层级)
    /// </summary>
    [JsonPropertyName("sections")]
    public List<DocumentSection>? Sections { get; set; }

    /// <summary>
    /// 页数 (PDF 专用)
    /// </summary>
    [JsonPropertyName("pageCount")]
    public int PageCount { get; set; }

    /// <summary>
    /// 是否包含表格
    /// </summary>
    [JsonPropertyName("hasTables")]
    public bool HasTables { get; set; }

    /// <summary>
    /// 是否包含图片
    /// </summary>
    [JsonPropertyName("hasImages")]
    public bool HasImages { get; set; }
}

/// <summary>
/// 文档章节结构
/// </summary>
public class DocumentSection
{
    /// <summary>
    /// 标题层级 (1-6)
    /// </summary>
    [JsonPropertyName("level")]
    public int Level { get; set; }

    /// <summary>
    /// 标题文本
    /// </summary>
    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// 起始行号
    /// </summary>
    [JsonPropertyName("startLine")]
    public int StartLine { get; set; }

    /// <summary>
    /// 结束行号
    /// </summary>
    [JsonPropertyName("endLine")]
    public int EndLine { get; set; }
}

/// <summary>
/// 表格内容 (Excel, CSV)
/// </summary>
public class TableDocumentContent : DocumentContent
{
    [JsonIgnore]
    public override string ContentType => "table";

    /// <summary>
    /// CSV 文本表示 (用于 LLM 输入)
    /// </summary>
    [JsonPropertyName("csvRepresentation")]
    public string CsvRepresentation { get; set; } = string.Empty;

    /// <summary>
    /// 表头列表
    /// </summary>
    [JsonPropertyName("headers")]
    public List<string> Headers { get; set; } = new();

    /// <summary>
    /// 数据行数
    /// </summary>
    [JsonPropertyName("rowCount")]
    public int RowCount { get; set; }

    /// <summary>
    /// 列数
    /// </summary>
    [JsonPropertyName("columnCount")]
    public int ColumnCount { get; set; }

    /// <summary>
    /// 表格数据的自然语言摘要
    /// </summary>
    [JsonPropertyName("summary")]
    public string? Summary { get; set; }

    /// <summary>
    /// Excel 多工作表名称
    /// </summary>
    [JsonPropertyName("sheetNames")]
    public List<string>? SheetNames { get; set; }
}

/// <summary>
/// 图片内容
/// </summary>
public class ImageDocumentContent : DocumentContent
{
    [JsonIgnore]
    public override string ContentType => "image";

    /// <summary>
    /// 图片 URL 或 Base64 DataUrl
    /// </summary>
    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;

    /// <summary>
    /// 缩略图 URL
    /// </summary>
    [JsonPropertyName("thumbnailUrl")]
    public string? ThumbnailUrl { get; set; }

    /// <summary>
    /// Vision 模型生成的图片描述
    /// </summary>
    [JsonPropertyName("description")]
    public string? Description { get; set; }

    /// <summary>
    /// 图片宽度
    /// </summary>
    [JsonPropertyName("width")]
    public int Width { get; set; }

    /// <summary>
    /// 图片高度
    /// </summary>
    [JsonPropertyName("height")]
    public int Height { get; set; }

    /// <summary>
    /// 图片格式: "jpeg", "png", "gif", "webp"
    /// </summary>
    [JsonPropertyName("format")]
    public string Format { get; set; } = string.Empty;
}

/// <summary>
/// 代码内容 (特化增强) - 重点支持 C#, Java, JavaScript, Python
/// </summary>
public class CodeDocumentContent : DocumentContent
{
    [JsonIgnore]
    public override string ContentType => "code";

    /// <summary>
    /// 完整源代码
    /// </summary>
    [JsonPropertyName("text")]
    public string Text { get; set; } = string.Empty;

    /// <summary>
    /// 编程语言: csharp, java, javascript, python, typescript 等
    /// </summary>
    [JsonPropertyName("language")]
    public string Language { get; set; } = string.Empty;

    /// <summary>
    /// 语言版本，例如: "C# 12", "Python 3.11", "Java 21"
    /// </summary>
    [JsonPropertyName("languageVersion")]
    public string? LanguageVersion { get; set; }

    /// <summary>
    /// 文件编码
    /// </summary>
    [JsonPropertyName("encoding")]
    public string Encoding { get; set; } = "utf-8";

    /// <summary>
    /// 代码结构 (类、方法、导入等)
    /// </summary>
    [JsonPropertyName("structure")]
    public CodeStructure? Structure { get; set; }

    /// <summary>
    /// 代码指标
    /// </summary>
    [JsonPropertyName("metrics")]
    public CodeMetrics? Metrics { get; set; }

    /// <summary>
    /// 语法问题列表 (错误、警告)
    /// </summary>
    [JsonPropertyName("issues")]
    public List<CodeIssue>? Issues { get; set; }
}

/// <summary>
/// 代码结构分析结果
/// </summary>
public class CodeStructure
{
    /// <summary>
    /// 导入语句列表
    /// </summary>
    [JsonPropertyName("imports")]
    public List<CodeImport> Imports { get; set; } = new();

    /// <summary>
    /// 函数/方法列表
    /// </summary>
    [JsonPropertyName("functions")]
    public List<CodeFunction> Functions { get; set; } = new();

    /// <summary>
    /// 类定义列表
    /// </summary>
    [JsonPropertyName("classes")]
    public List<CodeClass> Classes { get; set; } = new();
}

/// <summary>
/// 代码导入语句
/// </summary>
public class CodeImport
{
    [JsonPropertyName("module")]
    public string Module { get; set; } = string.Empty;

    [JsonPropertyName("alias")]
    public string? Alias { get; set; }

    [JsonPropertyName("line")]
    public int Line { get; set; }
}

/// <summary>
/// 代码函数/方法定义
/// </summary>
public class CodeFunction
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("parameters")]
    public List<string> Parameters { get; set; } = new();

    [JsonPropertyName("returnType")]
    public string? ReturnType { get; set; }

    [JsonPropertyName("lineStart")]
    public int LineStart { get; set; }

    [JsonPropertyName("lineEnd")]
    public int LineEnd { get; set; }

    [JsonPropertyName("isAsync")]
    public bool IsAsync { get; set; }

    [JsonPropertyName("docstring")]
    public string? Docstring { get; set; }
}

/// <summary>
/// 代码类定义
/// </summary>
public class CodeClass
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("methods")]
    public List<string> Methods { get; set; } = new();

    [JsonPropertyName("properties")]
    public List<string> Properties { get; set; } = new();

    [JsonPropertyName("baseClass")]
    public string? BaseClass { get; set; }

    [JsonPropertyName("interfaces")]
    public List<string> Interfaces { get; set; } = new();

    [JsonPropertyName("lineStart")]
    public int LineStart { get; set; }

    [JsonPropertyName("lineEnd")]
    public int LineEnd { get; set; }
}

/// <summary>
/// 代码指标
/// </summary>
public class CodeMetrics
{
    /// <summary>
    /// 总行数
    /// </summary>
    [JsonPropertyName("totalLines")]
    public int TotalLines { get; set; }

    /// <summary>
    /// 代码行数 (排除空行和注释)
    /// </summary>
    [JsonPropertyName("codeLines")]
    public int CodeLines { get; set; }

    /// <summary>
    /// 注释行数
    /// </summary>
    [JsonPropertyName("commentLines")]
    public int CommentLines { get; set; }

    /// <summary>
    /// 空行数
    /// </summary>
    [JsonPropertyName("blankLines")]
    public int BlankLines { get; set; }

    /// <summary>
    /// 圈复杂度
    /// </summary>
    [JsonPropertyName("complexity")]
    public int Complexity { get; set; }
}

/// <summary>
/// 代码问题 (语法错误、警告)
/// </summary>
public class CodeIssue
{
    /// <summary>
    /// 严重级别: "error", "warning", "info"
    /// </summary>
    [JsonPropertyName("severity")]
    public string Severity { get; set; } = "info";

    /// <summary>
    /// 问题描述
    /// </summary>
    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// 行号
    /// </summary>
    [JsonPropertyName("line")]
    public int Line { get; set; }

    /// <summary>
    /// 列号
    /// </summary>
    [JsonPropertyName("column")]
    public int Column { get; set; }
}
