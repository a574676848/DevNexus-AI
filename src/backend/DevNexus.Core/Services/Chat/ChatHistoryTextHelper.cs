using Microsoft.SemanticKernel.ChatCompletion;
using DevNexus.Shared.Constants;

namespace DevNexus.Core.Services.Chat;

internal static class ChatHistoryTextHelper
{
    public static int EstimateTokenCount(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return 0;
        }

        var chineseCount = text.Count(c => c >= 0x4E00 && c <= 0x9FFF);
        var otherCount = text.Length - chineseCount;

        return (int)Math.Ceiling(chineseCount / 1.5) + (int)Math.Ceiling(otherCount / 4.0);
    }

    public static string TruncateOutput(string output, int maxChars = 3000)
    {
        if (string.IsNullOrEmpty(output) || output.Length <= maxChars)
        {
            return output;
        }

        var head = maxChars / 2;
        var tail = maxChars - head - 100;
        return output[..head] + "\n\n... [中间内容已截断，为节省上下文预算只保留首尾] ...\n\n" + output[^tail..];
    }

    public static void AddMessageToChatHistory(ChatHistory chatHistory, string senderType, string content)
    {
        if (ChatConstants.IsUserSender(senderType))
        {
            chatHistory.AddUserMessage(content);
            return;
        }

        if (ChatConstants.IsAssistantSender(senderType))
        {
            chatHistory.AddAssistantMessage(content);
        }
    }
}