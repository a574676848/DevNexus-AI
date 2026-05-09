namespace DevNexus.Client.Shared.Models;

/// <summary>
/// 导航项配置
/// </summary>
public class NavItem
{
    /// <summary>
    /// 导航路径
    /// </summary>
    public required string Href { get; init; }

    /// <summary>
    /// 导航标题（工具提示）
    /// </summary>
    public required string Title { get; init; }

    /// <summary>
    /// 图标类型
    /// </summary>
    public required NavIconType IconType { get; init; }

    /// <summary>
    /// 排序顺序
    /// </summary>
    public int Order { get; init; }

    /// <summary>
    /// 是否需要特定权限
    /// </summary>
    public string? RequiredPermission { get; init; }
}

/// <summary>
/// 导航图标类型
/// </summary>
public enum NavIconType
{
    Chat,
    Analytics,
    ControlHub,
    UserManagement,
    Settings,
    Menu
}
