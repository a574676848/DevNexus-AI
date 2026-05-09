using System.Text.RegularExpressions;
// using DevNexus.Domain.Abstractions via GlobalUsings
using DevNexus.Shared.DTOs;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace DevNexus.Infrastructure.Services.Parsing;

/// <summary>
/// 本地代码分析服务 (默认实现)
/// </summary>
public class LocalCodeAnalysisService : ICodeAnalysisService
{
    private readonly ILogger<LocalCodeAnalysisService> _logger;

    private static class ChunkMetadataKeys
    {
        public const string Name = "Name";
        public const string Type = "Type";
        public const string LineStart = "LineStart";

        public const string TypeClass = "Class";
        public const string TypeFunction = "Function";
        public const string TypeFullFile = "FullFile";
    }

    public LocalCodeAnalysisService(ILogger<LocalCodeAnalysisService> logger)
    {
        _logger = logger;
    }

    public async Task<CodeAnalysisResult> AnalyzeCodeAsync(string code, string language)
    {
        var result = new CodeAnalysisResult
        {
            Metrics = CalculateMetrics(code)
        };

        CodeStructure structure;
        
        if (language.Equals("csharp", StringComparison.OrdinalIgnoreCase))
        {
            structure = ParseWithRoslyn(code);
        }
        else
        {
            structure = ParseWithRegex(code, language);
        }

        result.StructureJson = JsonSerializer.Serialize(structure);
        result.Chunks = GenerateChunks(code, structure, language);
        
        return await Task.FromResult(result);
    }

    private List<SmartChunk> GenerateChunks(string code, CodeStructure structure, string language)
    {
        var chunks = new List<SmartChunk>();
        var lines = code.Split('\n');

        // Strategy: Create a chunk for each class and function
        // This is a simplified chunking strategy. 
        
        foreach (var cls in structure.Classes)
        {
            var chunkContent = ExtractLines(lines, cls.LineStart, cls.LineEnd > 0 ? cls.LineEnd : cls.LineStart + 10);
            chunks.Add(new SmartChunk
            {
                Type = ChunkType.Code,
                Content = $"```{language}\n{chunkContent}\n```",
                StructuredData = JsonSerializer.Serialize(cls),
                Metadata = new Dictionary<string, object>
                {
                    { ChunkMetadataKeys.Name, cls.Name },
                    { ChunkMetadataKeys.Type, ChunkMetadataKeys.TypeClass },
                    { ChunkMetadataKeys.LineStart, cls.LineStart }
                }
            });
        }

        foreach (var func in structure.Functions)
        {
             var chunkContent = ExtractLines(lines, func.LineStart, func.LineEnd > 0 ? func.LineEnd : func.LineStart + 5);
             chunks.Add(new SmartChunk
             {
                 Type = ChunkType.Code,
                 Content = $"```{language}\n{chunkContent}\n```",
                 StructuredData = JsonSerializer.Serialize(func),
                 Metadata = new Dictionary<string, object>
                 {
                     { ChunkMetadataKeys.Name, func.Name },
                     { ChunkMetadataKeys.Type, ChunkMetadataKeys.TypeFunction },
                     { ChunkMetadataKeys.LineStart, func.LineStart }
                 }
             });
        }
        
        // If no structure found, fallback to single chunk
        if (!chunks.Any())
        {
            chunks.Add(new SmartChunk
            {
                Type = ChunkType.Code,
                Content = $"```{language}\n{code}\n```",
                Metadata = new Dictionary<string, object> { { ChunkMetadataKeys.Type, ChunkMetadataKeys.TypeFullFile } }
            });
        }

        return chunks;
    }
    
    private string ExtractLines(string[] lines, int start, int end)
    {
        start = Math.Max(0, start - 1);
        end = Math.Min(lines.Length, end);
        if (start >= end) return "";
        return string.Join("\n", lines.Skip(start).Take(end - start));
    }

    // --- Roslyn Logic (Migrated) ---
    private CodeStructure ParseWithRoslyn(string code)
    {
        var structure = new CodeStructure();
        try
        {
            var tree = CSharpSyntaxTree.ParseText(code);
            var root = tree.GetCompilationUnitRoot();

            foreach (var usingDirective in root.Usings)
            {
                structure.Imports.Add(new CodeImport
                {
                    Module = usingDirective.Name?.ToString() ?? "",
                    Alias = usingDirective.Alias?.Name.ToString(),
                    Line = usingDirective.GetLocation().GetLineSpan().StartLinePosition.Line + 1
                });
            }

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
                foreach (var method in classDecl.Members.OfType<MethodDeclarationSyntax>())
                    codeClass.Methods.Add(method.Identifier.Text);
                foreach (var prop in classDecl.Members.OfType<PropertyDeclarationSyntax>())
                    codeClass.Properties.Add(prop.Identifier.Text);
                if (classDecl.BaseList != null)
                   foreach (var baseType in classDecl.BaseList.Types.Skip(1))
                       codeClass.Interfaces.Add(baseType.Type.ToString());

                structure.Classes.Add(codeClass);
            }

            // Top-level methods
            var methods = root.DescendantNodes().OfType<MethodDeclarationSyntax>()
                .Where(m => m.Parent is not ClassDeclarationSyntax);
            foreach (var method in methods)
            {
                 structure.Functions.Add(new CodeFunction
                 {
                     Name = method.Identifier.Text,
                     ReturnType = method.ReturnType.ToString(),
                     IsAsync = method.Modifiers.Any(m => m.IsKind(SyntaxKind.AsyncKeyword)),
                     LineStart = method.GetLocation().GetLineSpan().StartLinePosition.Line + 1,
                     LineEnd = method.GetLocation().GetLineSpan().EndLinePosition.Line + 1
                 });
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Roslyn parsing failed, falling back to Regex");
            return ParseWithRegex(code, "csharp");
        }
        return structure;
    }

    // --- Regex Logic (Migrated) ---
    private CodeStructure ParseWithRegex(string code, string language)
    {
        var structure = new CodeStructure();
        var patterns = GetLanguagePatterns(language);
        
        // Imports
        if (patterns.ImportPattern != null)
        {
             var matches = Regex.Matches(code, patterns.ImportPattern, RegexOptions.Multiline);
             foreach (Match match in matches)
             {
                 var line = code[..match.Index].Count(c => c == '\n') + 1;
                 structure.Imports.Add(new CodeImport { Module = match.Groups[1].Value, Line = line });
             }
        }

        // Functions
        if (patterns.FunctionPattern != null)
        {
            var matches = Regex.Matches(code, patterns.FunctionPattern, RegexOptions.Multiline);
            foreach (Match match in matches)
            {
                var line = code[..match.Index].Count(c => c == '\n') + 1;
                structure.Functions.Add(new CodeFunction
                {
                    Name = match.Groups["name"].Success ? match.Groups["name"].Value : match.Groups[1].Value,
                    LineStart = line,
                    IsAsync = match.Value.Contains("async")
                });
            }
        }

        // Classes
        if (patterns.ClassPattern != null)
        {
            var matches = Regex.Matches(code, patterns.ClassPattern, RegexOptions.Multiline);
            foreach (Match match in matches)
            {
                var line = code[..match.Index].Count(c => c == '\n') + 1;
                structure.Classes.Add(new CodeClass
                {
                    Name = match.Groups["name"].Success ? match.Groups["name"].Value : match.Groups[1].Value,
                    LineStart = line
                });
            }
        }
        return structure;
    }

    private (string? ImportPattern, string? FunctionPattern, string? ClassPattern) GetLanguagePatterns(string language)
    {
        return language.ToLowerInvariant() switch
        {
            "java" => (
                @"^import\s+(.+?);",
                @"(?:public|private|protected)?\s*(?:static)?\s*(?:\w+)\s+(?<name>\w+)\s*\((?<params>[^)]*)\)\s*(?:throws\s+\w+)?\s*\{",
                @"(?:public|private|protected)?\s*(?:abstract|final)?\s*class\s+(?<name>\w+)"
            ),
            "javascript" or "typescript" or "js" or "ts" => (
                @"^(?:import\s+.*?from\s+['""](.+?)['""]|const\s+\w+\s*=\s*require\(['""](.+?)['""]\))",
                @"(?:async\s+)?(?:function\s+(?<name>\w+)|(?:const|let|var)\s+(?<name>\w+)\s*=\s*(?:async\s+)?\(?(?<params>[^)]*)\)?\s*=>|(?<name>\w+)\s*:\s*(?:async\s+)?(?:function)?\s*\((?<params>[^)]*)\))",
                @"class\s+(?<name>\w+)"
            ),
            "python" or "py" => (
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
    
    private Dictionary<string, object> CalculateMetrics(string code)
    {
         var lines = code.Split('\n');
         var total = lines.Length;
         var blank = lines.Count(string.IsNullOrWhiteSpace);
         return new Dictionary<string, object>
         {
             { "TotalLines", total },
             { "CodeLines", total - blank },
             { "Complexity", 1 } // Simplified
         };
    }
}
