namespace DevNexus.Client.Shared.Models;

/// <summary>
/// 快捷工具模型
/// 用于输入框工具面板中的能力入口定义。
/// </summary>
public class QuickCommand
{
    /// <summary>
    /// 指令标识（如 /explain）
    /// </summary>
    public string Command { get; init; } = string.Empty;
    
    /// <summary>
    /// 指令名称（如 "解释代码"）
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// 工具描述
    /// </summary>
    public string Description { get; init; } = string.Empty;
    
    /// <summary>
    /// 预设提示词模板
    /// </summary>
    public string Template { get; init; } = string.Empty;
    
    /// <summary>
    /// 分类标识
    /// </summary>
    public string Category { get; init; } = string.Empty;
    
    /// <summary>
    /// 分类显示名称
    /// </summary>
    public string CategoryName { get; init; } = string.Empty;
    
    /// <summary>
    /// 分类图标
    /// </summary>
    public string CategoryIcon { get; init; } = string.Empty;
    
    /// <summary>
    /// 指令图标
    /// </summary>
    public string Icon { get; init; } = "fa-solid fa-terminal";

    /// <summary>
    /// 工具操作文案
    /// </summary>
    public string ActionLabel { get; init; } = "使用工具";

    /// <summary>
    /// 激活工具后的输入提示
    /// </summary>
    public string Placeholder { get; init; } = string.Empty;

    /// <summary>
    /// 工具元数据
    /// </summary>
    public Dictionary<string, string> Metadata { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    
    /// <summary>
    /// 创建快捷指令
    /// </summary>
    public QuickCommand() { }
}

/// <summary>
/// 预设快捷指令集合
/// </summary>
public static class QuickCommands
{
    /// <summary>
    /// 所有预设工具
    /// </summary>
    public static readonly List<QuickCommand> All = new()
    {
        new()
        {
            Command = "/search",
            Name = "网络搜索",
            Description = "联网检索并在需要时继续读取网页正文。",
            Template = "请围绕以下主题执行网络搜索，并给出来源清晰的结论：\n- 主题：\n- 关注点：\n- 输出形式：",
            Category = "knowledge",
            CategoryName = "知识与研究",
            CategoryIcon = "fa-solid fa-book-open",
            Icon = "fa-solid fa-globe",
            ActionLabel = "启用搜索工具",
            Placeholder = "网络搜索工具已就绪，输入搜索主题或问题",
            Metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["toolId"] = "web-search",
                ["toolDisplayName"] = "网络搜索"
            }
        },
        new()
        {
            Command = "/research",
            Name = "深度研究",
            Description = "多轮高级搜索结合知识库查询，生成 HTML 风格研究报告。",
            Template = "请执行一份深度研究任务，并输出 HTML 风格研究报告：\n- 研究主题：\n- 研究目标：\n- 关键问题：\n- 交付重点：",
            Category = "knowledge",
            CategoryName = "知识与研究",
            CategoryIcon = "fa-solid fa-book-open",
            Icon = "fa-solid fa-microscope",
            ActionLabel = "启用研究工具",
            Placeholder = "深度研究工具已就绪，输入研究主题与交付要求",
            Metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["toolId"] = "deep-research",
                ["toolDisplayName"] = "深度研究"
            }
        },
        new()
        {
            Command = "/image-pro",
            Name = "专业文生图",
            Description = "扩写专业级视觉提示词并直接发起高质量出图。",
            Template = "请基于以下要求生成一张专业级图像，并先扩写为可执行的高质量提示词：\n- 主体：\n- 场景：\n- 风格：\n- 画幅比例：\n- 额外要求：",
            Category = "creative",
            CategoryName = "创作与生成",
            CategoryIcon = "fa-solid fa-wand-magic-sparkles",
            Icon = "fa-solid fa-image",
            ActionLabel = "启用出图工具",
            Placeholder = "专业文生图工具已就绪，输入主体、风格与画面要求",
            Metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["toolId"] = "professional-image",
                ["toolDisplayName"] = "专业文生图"
            }
        }
    };
    
    /// <summary>
    /// 按分类分组的指令
    /// </summary>
    public static IEnumerable<IGrouping<string, QuickCommand>> GroupedByCategory => 
        All.GroupBy(c => c.Category);
    
    /// <summary>
    /// 搜索指令
    /// </summary>
    public static IEnumerable<QuickCommand> Search(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return All;
            
        var lowerQuery = query.ToLower();
        return All.Where(c => 
            c.Command.Contains(lowerQuery, StringComparison.OrdinalIgnoreCase) ||
            c.Name.Contains(lowerQuery, StringComparison.OrdinalIgnoreCase) ||
            c.Description.Contains(lowerQuery, StringComparison.OrdinalIgnoreCase) ||
            c.CategoryName.Contains(lowerQuery, StringComparison.OrdinalIgnoreCase));
    }
}
