namespace DevNexus.Shared.Enums;

/// <summary>
/// 区块类型枚举，定义了AI输出的不同区块类型
/// </summary>
public enum BlockType
{
    /// <summary>
    /// 普通Markdown增量
    /// </summary>
    TextDelta,
    
    /// <summary>
    /// 思维链（:::thinking Block）
    /// </summary>
    Thinking,
    
    /// <summary>
    /// 独立文档开始（触发分屏预览）
    /// </summary>
    ArtifactStart,
    
    /// <summary>
    /// 独立文档增量内容
    /// </summary>
    ArtifactDelta,
    
    /// <summary>
    /// 独立文档结束
    /// </summary>
    ArtifactEnd,
    
    /// <summary>
    /// 交互卡片（如SQL审批、脚本执行、网页搜索）
    /// </summary>
    InteractiveCard,
    
    /// <summary>
    /// 图表数据（Plotly JSON格式）
    /// </summary>
    Chart,
    
    /// <summary>
    /// RAG 检索结果上下文（可折叠引用卡片）
    /// </summary>
    RagContext,
    
    /// <summary>
    /// 警告/提示信息（:::warning Block）
    /// </summary>
    Warning,
    
    /// <summary>
    /// 工具调用结果（:::tool-result Block）
    /// </summary>
    ToolResult,
    
    /// <summary>
    /// 引用已有 Artifact（:::ref Block）
    /// </summary>
    Reference,
    
    /// <summary>
    /// 回复被截断通知（max_tokens 限制，前端显示"继续生成"按钮）
    /// </summary>
    Truncated,

    /// <summary>
    /// 终端执行区块 (Phase 7)
    /// </summary>
    Terminal
}
