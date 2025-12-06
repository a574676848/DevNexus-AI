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
    /// 思考过程（前端渲染为折叠面板）
    /// </summary>
    ThoughtChain,
    
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
    Chart
}
