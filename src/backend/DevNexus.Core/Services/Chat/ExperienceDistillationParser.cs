namespace DevNexus.Core.Services.Chat;

/// <summary>
/// 系统经验提纯输出解析器。
/// </summary>
public static class ExperienceDistillationParser
{
    /// <summary>
    /// 解析模型输出。
    /// </summary>
    public static ExperienceDistillationParseResult Parse(string? content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return NoValue(ExperienceDistillationParseReasons.Empty);
        }

        var normalized = content.Trim();
        if (ContainsMarkdownCodeBlock(normalized))
        {
            return NoValue(ExperienceDistillationParseReasons.MarkdownCodeBlock);
        }

        if (IsNoValue(normalized))
        {
            return NoValue(ExperienceDistillationParseReasons.NoValue);
        }

        if (StartsWithNoValue(normalized))
        {
            return NoValue(ExperienceDistillationParseReasons.NoValueWithContent);
        }

        var lines = normalized
            .Split('\n', 2)
            .Select(line => line.Trim('\r', ' '))
            .ToArray();
        if (lines.Length < 2)
        {
            return NoValue(ExperienceDistillationParseReasons.MissingSop);
        }

        if (!lines[0].StartsWith(ExperienceDistillationOutputProtocol.IntentMarker, StringComparison.OrdinalIgnoreCase))
        {
            return NoValue(ExperienceDistillationParseReasons.MissingIntentMarker);
        }

        var intent = NormalizeIntent(lines[0]);
        var sop = lines[1].Trim();
        if (string.IsNullOrWhiteSpace(intent))
        {
            return NoValue(ExperienceDistillationParseReasons.MissingIntent);
        }

        if (string.IsNullOrWhiteSpace(sop))
        {
            return NoValue(ExperienceDistillationParseReasons.MissingSop);
        }

        if (sop.Length > ExperienceDistillationOutputProtocol.MaximumSopCharacters)
        {
            return NoValue(ExperienceDistillationParseReasons.SopTooLong);
        }

        if (ContainsRawTranscriptMarker(sop))
        {
            return NoValue(ExperienceDistillationParseReasons.RawTranscriptLeak);
        }

        return new ExperienceDistillationParseResult
        {
            HasValue = true,
            Intent = intent,
            SolutionSop = sop,
            Reason = ExperienceDistillationParseReasons.ValueExtracted
        };
    }

    private static bool ContainsMarkdownCodeBlock(string content)
    {
        return content.Contains("```", StringComparison.Ordinal);
    }

    private static bool ContainsRawTranscriptMarker(string content)
    {
        return ExperienceDistillationOutputProtocol.RawTranscriptMarkers
            .Any(marker => content.Contains(marker, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsNoValue(string content)
    {
        return string.Equals(
            content,
            ExperienceDistillationOutputProtocol.NoValueMarker,
            StringComparison.OrdinalIgnoreCase);
    }

    private static bool StartsWithNoValue(string content)
    {
        return content.StartsWith(
            ExperienceDistillationOutputProtocol.NoValueMarker,
            StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeIntent(string line)
    {
        return line
            .Replace(ExperienceDistillationOutputProtocol.IntentMarker, string.Empty, StringComparison.OrdinalIgnoreCase)
            .Trim()
            .TrimStart(':', '：')
            .Trim();
    }

    private static ExperienceDistillationParseResult NoValue(string reason)
    {
        return new ExperienceDistillationParseResult { Reason = reason };
    }
}
