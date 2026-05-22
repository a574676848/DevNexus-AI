namespace DevNexus.Core.Services.Chat;

/// <summary>
/// 系统经验提纯准入策略。
/// </summary>
public static class ExperienceDistillationAdmissionPolicy
{
    /// <summary>
    /// 最小用户问题长度。
    /// </summary>
    public const int MinimumQuestionLength = 10;

    /// <summary>
    /// 最小助手回答长度。
    /// </summary>
    public const int MinimumAnswerLength = 30;

    /// <summary>
    /// 判断 QA 是否允许进入系统经验提纯。
    /// </summary>
    public static ExperienceDistillationAdmissionDecision Decide(string? question, string? answer)
    {
        if (string.IsNullOrWhiteSpace(question) || string.IsNullOrWhiteSpace(answer))
        {
            return Reject(ExperienceDistillationAdmissionReasons.MissingQaPair);
        }

        if (question.Trim().Length < MinimumQuestionLength || answer.Trim().Length < MinimumAnswerLength)
        {
            return Reject(ExperienceDistillationAdmissionReasons.ContentTooShort);
        }

        var text = string.Concat(question, Environment.NewLine, answer);
        var matchedSkipKeyword = FindFirstMatch(text, ExperienceDistillationOutputProtocol.SkipConditionKeywords);
        if (!string.IsNullOrWhiteSpace(matchedSkipKeyword))
        {
            return Reject(
                ExperienceDistillationAdmissionReasons.SkipConditionMatched,
                matchedSkipKeyword: matchedSkipKeyword);
        }

        var matchedValueKeyword = FindFirstMatch(text, ExperienceDistillationOutputProtocol.HighValueSignalKeywords);
        if (string.IsNullOrWhiteSpace(matchedValueKeyword))
        {
            return Reject(ExperienceDistillationAdmissionReasons.MissingValueSignal);
        }

        return new ExperienceDistillationAdmissionDecision
        {
            ShouldDistill = true,
            Reason = ExperienceDistillationAdmissionReasons.Accepted,
            MatchedValueSignalKeyword = matchedValueKeyword
        };
    }

    private static ExperienceDistillationAdmissionDecision Reject(
        string reason,
        string matchedSkipKeyword = "")
    {
        return new ExperienceDistillationAdmissionDecision
        {
            Reason = reason,
            MatchedSkipConditionKeyword = matchedSkipKeyword
        };
    }

    private static string FindFirstMatch(string text, IReadOnlyList<string> keywords)
    {
        return keywords.FirstOrDefault(keyword =>
            text.Contains(keyword, StringComparison.OrdinalIgnoreCase)) ?? string.Empty;
    }
}
