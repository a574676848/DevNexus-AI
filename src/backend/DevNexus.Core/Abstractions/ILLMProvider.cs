using Microsoft.SemanticKernel.ChatCompletion;

namespace DevNexus.Core.Abstractions;

/// <summary>
/// LLM 提供商抽象接口
/// 支持多提供商切换（OpenAI Compatible / Gemini / Kimi / MiniMax）
/// </summary>
public interface ILLMProvider
{
    /// <summary>
    /// 提供商名称
    /// </summary>
    string ProviderName { get; }

    /// <summary>
    /// 是否支持 Semantic Kernel 自动函数调用。
    /// </summary>
    bool SupportsAutoFunctionCalling { get; }
    
    /// <summary>
    /// 获取聊天完成服务实例
    /// </summary>
    /// <returns>聊天完成服务</returns>
    IChatCompletionService GetChatCompletionService();
}
