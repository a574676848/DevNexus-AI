using DevNexus.Shared.Constants;

namespace DevNexus.Core.Services.Chat;

/// <summary>
/// 经验提纯问答对选择器。
/// </summary>
public static class ExperienceDistillationQaPairSelector
{
    /// <summary>
    /// 选择最近相邻的用户-助手问答对。
    /// </summary>
    public static ExperienceDistillationQaPair? SelectLatestCompletedPair(
        IReadOnlyList<ExperienceDistillationQaMessage> messages)
    {
        for (var index = messages.Count - 1; index > 0; index--)
        {
            var answer = messages[index];
            var question = messages[index - 1];
            if (!ChatConstants.IsAssistantSender(answer.SenderType)
                || !ChatConstants.IsUserSender(question.SenderType))
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(question.Text) || string.IsNullOrWhiteSpace(answer.Text))
            {
                continue;
            }

            return new ExperienceDistillationQaPair
            {
                Question = question.Text,
                Answer = answer.Text
            };
        }

        return null;
    }
}
