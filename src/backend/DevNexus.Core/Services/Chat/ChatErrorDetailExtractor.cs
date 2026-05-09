namespace DevNexus.Core.Services.Chat;

/// <summary>
/// 聊天异常明细提取器。
/// 负责从异常对象中提取更适合用户与日志展示的错误摘要。
/// </summary>
public static class ChatErrorDetailExtractor
{
    /// <summary>
    /// 提取异常详情。
    /// </summary>
    public static string Extract(Exception ex)
    {
        if (ex is System.ClientModel.ClientResultException clientEx)
        {
            return $"API 错误 (状态码: {clientEx.Status}): {clientEx.Message}";
        }

        var innerMessage = ex.InnerException?.Message;
        if (!string.IsNullOrEmpty(innerMessage))
        {
            return $"{ex.Message} -> {innerMessage}";
        }

        return ex.Message;
    }
}
