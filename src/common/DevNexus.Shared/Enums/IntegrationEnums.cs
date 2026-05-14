namespace DevNexus.Shared.Enums;

/// <summary>
/// 外部系统集成类型
/// </summary>
public enum IntegrationType
{
    /// <summary>
    /// 代码仓库 (GitHub, GitLab, Gitea)
    /// </summary>
    CodeRepository = 2,

    /// <summary>
    /// 云存储 (Google Drive, OneDrive, Dropbox)
    /// </summary>
    CloudStorage = 3,

    /// <summary>
    /// 项目管理 (Jira, Trello, Asana)
    /// </summary>
    ProjectManagement = 4,

    /// <summary>
    /// 通讯工具 (Slack, Discord, Telegram)
    /// </summary>
    Communication = 5,

    /// <summary>
    /// 日历服务 (Google Calendar, Outlook Calendar)
    /// </summary>
    Calendar = 6,

    /// <summary>
    /// 邮件服务 (Gmail, Outlook)
    /// </summary>
    Email = 7,

    /// <summary>
    /// CI/CD 工具 (Jenkins, GitHub Actions)
    /// </summary>
    CICD = 8,

    /// <summary>
    /// 监控服务 (Grafana, Datadog)
    /// </summary>
    Monitoring = 9,

    /// <summary>
    /// 其他自定义集成
    /// </summary>
    Custom = 99
}

/// <summary>
/// 集成认证类型
/// </summary>
public enum IntegrationAuthType
{
    /// <summary>
    /// API Token / Access Token
    /// </summary>
    ApiToken = 1,

    /// <summary>
    /// OAuth 2.0
    /// </summary>
    OAuth2 = 2,

    /// <summary>
    /// API Key + Secret
    /// </summary>
    ApiKeySecret = 3,

    /// <summary>
    /// 用户名 + 密码
    /// </summary>
    UsernamePassword = 4,

    /// <summary>
    /// JWT Token
    /// </summary>
    JWT = 5,

    /// <summary>
    /// SSH Key
    /// </summary>
    SSHKey = 6
}
