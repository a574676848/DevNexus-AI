using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

namespace DevNexus.Core.Extensions;

public static class ChatCompletionServiceExtensions
{
    /// <summary>
    /// 支持自动无缝续写的 Chat 补全调用。
    /// 当大模型由于 max_tokens 限制而中断时（FinishReason == "Length"），
    /// 自动在后台注入续写指令并再次调用，直到完成（FinishReason == "Stop"）或达到最大重试次数。
    /// 这个实现使得系统能够一次性生成远超单次请求限制（如 4k/8k tokens）的长代码或长文文本。
    /// </summary>
    /// <param name="chatService">Semantic Kernel ChatCompletionService 实例</param>
    /// <param name="history">聊天记录（会在续写过程中被修改累积）</param>
    /// <param name="executionSettings">执行设置（通常包含 max_tokens 等参数）</param>
    /// <param name="kernel">SK Kernel 实例</param>
    /// <param name="logger">日志记录器实例（用于输出续写日志）</param>
    /// <param name="contextName">上下文名称，用于日志记录（比如 TaskId）</param>
    /// <param name="maxContinuations">最大自动续写次数。默认 10 次，足够生成数万行代码防止死循环。</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>合并后完整的响应内容</returns>
    public static async Task<string> GetAutoContinuedChatMessageContentAsync(
        this IChatCompletionService chatService,
        ChatHistory history,
        PromptExecutionSettings? executionSettings,
        Kernel? kernel,
        ILogger logger,
        string contextName = "Default",
        int maxContinuations = 20,
        CancellationToken cancellationToken = default)
    {
        int continuationCount = 0;
        bool isTruncated = true;
        string fullResult = string.Empty;

        while (isTruncated && continuationCount <= maxContinuations)
        {
            var resultMsg = await chatService.GetChatMessageContentAsync(history, executionSettings, kernel, cancellationToken);
            var currentContent = resultMsg.Content ?? string.Empty;
            fullResult += currentContent;

            // 检测是否由于 max_tokens 发生截断
            if (resultMsg.Metadata?.TryGetValue("FinishReason", out var fr) == true && fr?.ToString() == "Length")
            {
                isTruncated = true;
                continuationCount++;
                logger.LogWarning("[Auto-Continue] {ContextName} response truncated by max_tokens (Continuation {Count}/{Max}). Auto-resuming...", 
                    contextName, continuationCount, maxContinuations);
                        
                // 将当前的未写完的内容加入历史记录（让模型知道自己写到哪了）
                history.AddAssistantMessage(currentContent);
                
                // 发送继续生成的系统级指令
                history.AddUserMessage("你的上一条回复被长度限制截断了。请直接继续往下写，紧接着你最后输出的字符，不要重复前文，也不要说任何无关的废话。");
            }
            else
            {
                isTruncated = false;
            }
        }
        
        if (isTruncated)
        {
            logger.LogWarning("[Auto-Continue] {ContextName} hit maximum continuation limit ({Max}). Appending truncation notice.", contextName, maxContinuations);
            fullResult += "\n\n[TRUNCATED: maximum automatic continuations reached]";
        }

        return fullResult;
    }
}
