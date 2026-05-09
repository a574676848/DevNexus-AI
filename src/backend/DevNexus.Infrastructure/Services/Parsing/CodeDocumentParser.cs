using System.Security.Cryptography;
using System.Text;
// using DevNexus.Domain.Abstractions via GlobalUsings
using DevNexus.Shared.DTOs;
using Microsoft.Extensions.Logging;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Text.Json;

namespace DevNexus.Infrastructure.Services.Parsing;

/// <summary>
/// 代码文档解析器
/// 重点支持 C#、Java、JavaScript、Python
/// 使用 Roslyn 解析 C#，正则匹配其他语言
/// </summary>
public class CodeDocumentParser : ISmartDocumentParser
{
    private readonly ILogger<CodeDocumentParser> _logger;

    // 支持的代码文件 MIME 类型
    private static readonly HashSet<string> SupportedMimeTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "text/x-csharp", "text/csharp",              // C#
        "text/x-java", "text/java",                  // Java
        "application/javascript", "text/javascript", // JavaScript
        "text/typescript",                           // TypeScript
        "text/x-python", "text/python"               // Python
    };

    // 扩展名到语言的映射
    private static readonly Dictionary<string, string> ExtensionLanguageMap = new(StringComparer.OrdinalIgnoreCase)
    {
        { ".cs", "csharp" },
        { ".java", "java" },
        { ".js", "javascript" },
        { ".jsx", "javascript" },
        { ".ts", "typescript" },
        { ".tsx", "typescript" },
        { ".py", "python" }
    };

    private readonly ICodeAnalysisService _analysisService;

    public CodeDocumentParser(
        ILogger<CodeDocumentParser> logger,
        ICodeAnalysisService analysisService)
    {
        _logger = logger;
        _analysisService = analysisService;
    }

    public bool CanParse(string mimeType)
    {
        return SupportedMimeTypes.Contains(mimeType);
    }

    public async Task<SmartDocument> ParseAsync(
        Stream fileStream, 
        string fileName, 
        string? mimeType = null, 
        ParsingOptions? options = null,
        ParsingContext? context = null)
    {
        var startTime = DateTime.UtcNow;
        
        using var reader = new StreamReader(fileStream);
        var content = await reader.ReadToEndAsync();
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        var language = GetLanguage(extension);

        _logger.LogDebug("Parsing Code File: {FileName}, Language: {Language}", fileName, language);

        // Delegate to Analysis Service
        var analysis = await _analysisService.AnalyzeCodeAsync(content, language);

        var processingTime = DateTime.UtcNow - startTime;

        return new SmartDocument
        {
            FileId = Guid.NewGuid().ToString(),
            FileName = fileName,
            MimeType = mimeType ?? $"text/x-{language}",
            SizeBytes = Encoding.UTF8.GetByteCount(content),
            ContentHash = ComputeHash(content),
            CreatedAt = DateTime.UtcNow,
            ParsedAt = DateTime.UtcNow,
            // Populate Chunks from Analysis Result
            Chunks = analysis.Chunks,
            Content = new CodeDocumentContent
            {
                Text = content,
                Language = language,
                Encoding = "utf-8",
                 // Metrics and Structure are now in Chunks or Analysis Result
                Structure = !string.IsNullOrEmpty(analysis.StructureJson) 
                    ? JsonSerializer.Deserialize<CodeStructure>(analysis.StructureJson) 
                    : new CodeStructure(),
                Metrics = analysis.Metrics.ContainsKey("Metrics") 
                     ? JsonSerializer.Deserialize<CodeMetrics>(analysis.Metrics["Metrics"].ToString() ?? "{}")
                     : new CodeMetrics()
            },
            ParseInfo = new ParseMetadata
            {
                Strategy = "ICodeAnalysisService",
                ProcessingTimeMs = processingTime.TotalMilliseconds,
                QualityScore = 1.0,
                ParsedBy = "server"
            }
        };
    }

    /// <summary>
    /// 使用 Roslyn 解析 C# 代码
    /// </summary>
    private CodeStructure ParseWithRoslyn(string code)
    {
        var structure = new CodeStructure();

        try
        {
            var tree = CSharpSyntaxTree.ParseText(code);
            var root = tree.GetCompilationUnitRoot();

            // 提取 using 语句
            foreach (var usingDirective in root.Usings)
            {
                structure.Imports.Add(new CodeImport
                {
                    Module = usingDirective.Name?.ToString() ?? "",
                    Alias = usingDirective.Alias?.Name.ToString(),
                    Line = usingDirective.GetLocation().GetLineSpan().StartLinePosition.Line + 1
                });
            }

            // 提取类定义
            var classes = root.DescendantNodes().OfType<ClassDeclarationSyntax>();
            foreach (var classDecl in classes)
            {
                var codeClass = new CodeClass
                {
                    Name = classDecl.Identifier.Text,
                    BaseClass = classDecl.BaseList?.Types.FirstOrDefault()?.ToString(),
                    LineStart = classDecl.GetLocation().GetLineSpan().StartLinePosition.Line + 1,
                    LineEnd = classDecl.GetLocation().GetLineSpan().EndLinePosition.Line + 1
                };

                // 提取方法
                foreach (var method in classDecl.Members.OfType<MethodDeclarationSyntax>())
                {
                    codeClass.Methods.Add(method.Identifier.Text);
                }

                // 提取属性
                foreach (var prop in classDecl.Members.OfType<PropertyDeclarationSyntax>())
                {
                    codeClass.Properties.Add(prop.Identifier.Text);
                }

                // 提取接口
                if (classDecl.BaseList != null)
                {
                    foreach (var baseType in classDecl.BaseList.Types.Skip(1))
                    {
                        codeClass.Interfaces.Add(baseType.Type.ToString());
                    }
                }

                structure.Classes.Add(codeClass);
            }

            // 提取顶层方法（不在类中的）
            var methods = root.DescendantNodes().OfType<MethodDeclarationSyntax>()
                .Where(m => m.Parent is not ClassDeclarationSyntax);
            foreach (var method in methods)
            {
                structure.Functions.Add(new CodeFunction
                {
                    Name = method.Identifier.Text,
                    ReturnType = method.ReturnType.ToString(),
                    Parameters = method.ParameterList.Parameters
                        .Select(p => $"{p.Type} {p.Identifier}").ToList(),
                    IsAsync = method.Modifiers.Any(m => m.IsKind(SyntaxKind.AsyncKeyword)),
                    LineStart = method.GetLocation().GetLineSpan().StartLinePosition.Line + 1,
                    LineEnd = method.GetLocation().GetLineSpan().EndLinePosition.Line + 1
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Roslyn 解析失败，回退到正则解析");
            return ParseWithRegex(code, "csharp");
        }

        return structure;
    }

    /// <summary>
    /// 使用正则表达式解析其他语言代码
    /// </summary>
    private CodeStructure ParseWithRegex(string code, string language)
    {
        var structure = new CodeStructure();
        var lines = code.Split('\n');

        // 根据语言选择正则模式
        var patterns = GetLanguagePatterns(language);

        // 解析导入语句
        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i].Trim();
            if (patterns.ImportPattern != null && System.Text.RegularExpressions.Regex.IsMatch(line, patterns.ImportPattern))
            {
                var match = System.Text.RegularExpressions.Regex.Match(line, patterns.ImportPattern);
                structure.Imports.Add(new CodeImport
                {
                    Module = match.Groups[1].Value,
                    Line = i + 1
                });
            }
        }

        // 解析函数/方法
        if (patterns.FunctionPattern != null)
        {
            var matches = System.Text.RegularExpressions.Regex.Matches(code, patterns.FunctionPattern, System.Text.RegularExpressions.RegexOptions.Multiline);
            foreach (System.Text.RegularExpressions.Match match in matches)
            {
                var lineNumber = code[..match.Index].Count(c => c == '\n') + 1;
                structure.Functions.Add(new CodeFunction
                {
                    Name = match.Groups["name"].Success ? match.Groups["name"].Value : match.Groups[1].Value,
                    Parameters = ExtractParameters(match.Groups["params"].Success ? match.Groups["params"].Value : ""),
                    LineStart = lineNumber,
                    IsAsync = match.Value.Contains("async")
                });
            }
        }

        // 解析类
        if (patterns.ClassPattern != null)
        {
            var matches = System.Text.RegularExpressions.Regex.Matches(code, patterns.ClassPattern, System.Text.RegularExpressions.RegexOptions.Multiline);
            foreach (System.Text.RegularExpressions.Match match in matches)
            {
                var lineNumber = code[..match.Index].Count(c => c == '\n') + 1;
                structure.Classes.Add(new CodeClass
                {
                    Name = match.Groups["name"].Success ? match.Groups["name"].Value : match.Groups[1].Value,
                    LineStart = lineNumber
                });
            }
        }

        return structure;
    }

    /// <summary>
    /// 获取语言对应的正则模式
    /// </summary>
    private (string? ImportPattern, string? FunctionPattern, string? ClassPattern) GetLanguagePatterns(string language)
    {
        return language switch
        {
            "java" => (
                @"^import\s+(.+?);",
                @"(?:public|private|protected)?\s*(?:static)?\s*(?:\w+)\s+(?<name>\w+)\s*\((?<params>[^)]*)\)\s*(?:throws\s+\w+)?\s*\{",
                @"(?:public|private|protected)?\s*(?:abstract|final)?\s*class\s+(?<name>\w+)"
            ),
            "javascript" or "typescript" => (
                @"^(?:import\s+.*?from\s+['""](.+?)['""]|const\s+\w+\s*=\s*require\(['""](.+?)['""]\))",
                @"(?:async\s+)?(?:function\s+(?<name>\w+)|(?:const|let|var)\s+(?<name>\w+)\s*=\s*(?:async\s+)?\(?(?<params>[^)]*)\)?\s*=>|(?<name>\w+)\s*:\s*(?:async\s+)?(?:function)?\s*\((?<params>[^)]*)\))",
                @"class\s+(?<name>\w+)"
            ),
            "python" => (
                @"^(?:from\s+(\S+)\s+import|import\s+(\S+))",
                @"(?:async\s+)?def\s+(?<name>\w+)\s*\((?<params>[^)]*)\)",
                @"class\s+(?<name>\w+)"
            ),
            "csharp" => (
                @"^using\s+(.+?);",
                @"(?:public|private|protected|internal)?\s*(?:static)?\s*(?:async)?\s*(?:\w+\??)\s+(?<name>\w+)\s*\((?<params>[^)]*)\)",
                @"(?:public|private|protected|internal)?\s*(?:abstract|sealed|static|partial)?\s*class\s+(?<name>\w+)"
            ),
            _ => (null, null, null)
        };
    }

    /// <summary>
    /// 提取参数列表
    /// </summary>
    private List<string> ExtractParameters(string paramsString)
    {
        if (string.IsNullOrWhiteSpace(paramsString))
            return new List<string>();

        return paramsString
            .Split(',')
            .Select(p => p.Trim())
            .Where(p => !string.IsNullOrEmpty(p))
            .ToList();
    }

    /// <summary>
    /// 计算代码指标
    /// </summary>
    private CodeMetrics CalculateMetrics(string code)
    {
        var lines = code.Split('\n');
        var totalLines = lines.Length;
        var blankLines = lines.Count(l => string.IsNullOrWhiteSpace(l));
        var commentLines = lines.Count(l => 
        {
            var trimmed = l.Trim();
            return trimmed.StartsWith("//") || 
                   trimmed.StartsWith("/*") || 
                   trimmed.StartsWith("*") || 
                   trimmed.StartsWith("#");
        });

        return new CodeMetrics
        {
            TotalLines = totalLines,
            CodeLines = totalLines - blankLines - commentLines,
            CommentLines = commentLines,
            BlankLines = blankLines,
            Complexity = EstimateComplexity(code)
        };
    }

    /// <summary>
    /// 估算圈复杂度
    /// </summary>
    private int EstimateComplexity(string code)
    {
        // 简单估算：计算条件语句和循环的数量
        var keywords = new[] { "if", "else", "for", "foreach", "while", "switch", "case", "catch", "?", "&&", "||" };
        return keywords.Sum(kw => System.Text.RegularExpressions.Regex.Matches(code, $@"\b{kw}\b").Count) + 1;
    }

    /// <summary>
    /// 获取语言
    /// </summary>
    private string GetLanguage(string extension)
    {
        return ExtensionLanguageMap.TryGetValue(extension, out var lang) ? lang : "text";
    }

    /// <summary>
    /// 计算内容哈希
    /// </summary>
    private string ComputeHash(string content)
    {
        var bytes = Encoding.UTF8.GetBytes(content);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
