namespace DevNexus.Client.Shared.Services.Chat;

/// <summary>
/// ChatHub 连接恢复动作。
/// </summary>
public enum ChatConnectionRecoveryAction
{
    RenderOnly,
    RefreshRuntime,
    RecoverSession
}

/// <summary>
/// 聊天连接恢复决策。
/// </summary>
public static class ChatConnectionRecoveryPolicy
{
    public static ChatConnectionRecoveryAction ResolveConnectedAction(
        bool needsConnectionRecovery,
        Guid currentSessionId)
    {
        if (currentSessionId == Guid.Empty)
        {
            return ChatConnectionRecoveryAction.RenderOnly;
        }

        return needsConnectionRecovery
            ? ChatConnectionRecoveryAction.RecoverSession
            : ChatConnectionRecoveryAction.RefreshRuntime;
    }
}
