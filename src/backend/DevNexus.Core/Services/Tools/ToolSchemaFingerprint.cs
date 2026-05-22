using System.Text;
using DevNexus.Core.Services.Chat;
using DevNexus.Shared.DTOs;

namespace DevNexus.Core.Services.Tools;

/// <summary>
/// 工具 Schema 指纹生成器。
/// </summary>
public static class ToolSchemaFingerprint
{
    internal const string ProtocolVersion = "tool-invocation-protocol:v2";

    private const char FieldSeparator = '\u001F';
    private const char RecordSeparator = '\u001E';

    /// <summary>
    /// 计算稳定工具 Schema 指纹。
    /// </summary>
    public static string ComputeHash(IEnumerable<ToolCatalogItemDto> tools)
    {
        return PromptFingerprint.ComputeHash(BuildCanonicalSchema(tools));
    }

    /// <summary>
    /// 构建稳定 Schema 文本，供测试和审计使用。
    /// </summary>
    internal static string BuildCanonicalSchema(IEnumerable<ToolCatalogItemDto> tools)
    {
        var builder = new StringBuilder(ProtocolVersion);
        foreach (var tool in tools.OrderBy(tool => tool.PluginName, StringComparer.Ordinal))
        {
            builder.Append(RecordSeparator);

            AppendField(builder, tool.PluginName);
            AppendField(builder, tool.DisplayName);
            AppendField(builder, tool.Category);
            AppendField(builder, tool.RiskLevel);
            AppendField(builder, tool.ExposureMode);
            AppendField(builder, NormalizeAliases(tool.Aliases));
            AppendField(builder, tool.RequiresTaggedOutput ? "tagged-output:required" : "tagged-output:optional");
            AppendField(builder, tool.SupportsParallelExecution ? "parallel:supported" : "parallel:unsupported");
            builder.Append(Normalize(tool.ResultContract));
        }

        return builder.ToString();
    }

    private static void AppendField(StringBuilder builder, string value)
    {
        builder.Append(Normalize(value));
        builder.Append(FieldSeparator);
    }

    private static string Normalize(string value)
    {
        return value.Trim().ReplaceLineEndings("\n");
    }

    private static string NormalizeAliases(IEnumerable<string> aliases)
    {
        return string.Join(
            "\n",
            aliases
                .Where(alias => !string.IsNullOrWhiteSpace(alias))
                .Select(Normalize)
                .OrderBy(alias => alias, StringComparer.Ordinal));
    }
}
