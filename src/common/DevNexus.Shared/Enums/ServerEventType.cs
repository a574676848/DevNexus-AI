namespace DevNexus.Shared.Enums;

/// <summary>
/// 服务器事件类型枚举，定义了服务器发送给客户端的事件类型
/// </summary>
public enum ServerEventType
{
    /// <summary>
    /// 接收区块数据
    /// </summary>
    ReceiveBlock,
    
    /// <summary>
    /// 生成开始
    /// </summary>
    GenerationStarted,
    
    /// <summary>
    /// 生成结束
    /// </summary>
    GenerationCompleted,
    
    /// <summary>
    /// 生成被打断
    /// </summary>
    GenerationCancelled,
    
    /// <summary>
    /// 实时终端输出
    /// </summary>
    ConsoleOutput,
    
    /// <summary>
    /// 脚本执行结果
    /// </summary>
    ScriptExecutionResult,
    
    /// <summary>
    /// 系统通知
    /// </summary>
    SystemNotification
}
