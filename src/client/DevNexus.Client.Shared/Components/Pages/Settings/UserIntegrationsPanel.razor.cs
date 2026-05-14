using DevNexus.Shared.DTOs;
using DevNexus.Shared.DTOs.Auth;
using DevNexus.Shared.Enums;
using Microsoft.AspNetCore.Components;

namespace DevNexus.Client.Shared.Components.Pages.Settings;

/// <summary>
/// 用户集成管理面板代码后置
/// 处理所有逻辑，UI 由 Razor 子组件负责
/// </summary>
public partial class UserIntegrationsPanel
{
    [Parameter, EditorRequired]
    public bool IsAdmin { get; set; }

    [Parameter]
    public List<UserIntegrationResponse> UserIntegrations { get; set; } = new();

    [Parameter]
    public List<UserIntegrationDetailedResponse> AllIntegrations { get; set; } = new();

    [Parameter]
    public List<UserListItem> AvailableUsers { get; set; } = new();

    [Parameter]
    public EventCallback OnRefresh { get; set; }

    // 对话框状态
    protected bool ShowDialog { get; set; }
    protected bool ShowEditDialog { get; set; }
    protected bool ShowDeleteConfirm { get; set; }
    protected bool IsSubmitting { get; set; }

    // 创建对话框字段
    protected CreateUserIntegrationRequest NewIntegration { get; set; } = new();
    protected string SelectedIntegrationType { get; set; } = "";
    protected string SelectedAuthType { get; set; } = "";
    protected string SelectedProviderId { get; set; } = "";
    protected string SelectedUserId { get; set; } = "";
    protected string? DialogMessage { get; set; } = "";
    protected bool DialogSuccess { get; set; }

    // 编辑对话框字段
    protected UpdateUserIntegrationRequest EditIntegration { get; set; } = new();
    protected UserIntegrationResponse? IntegrationToEdit { get; set; }
    protected string? EditDialogMessage { get; set; } = "";
    protected bool EditDialogSuccess { get; set; }

    // 删除确认字段
    protected object? IntegrationToDelete { get; set; }
    protected string DeleteConfirmMessage { get; set; } = "";

    protected override async Task OnInitializedAsync()
    {
        NewIntegration.IsActive = true;
        
        if (IsAdmin)
        {
            await LoadUsers();
        }
    }

    /// <summary>
    /// 加载用户列表（仅管理员）
    /// </summary>
    protected async Task LoadUsers()
    {
        try
        {
            var response = await ApiService.GetUsersAsync(1, 100);
            // 使用参数中的 AvailableUsers，不在这里赋值
        }
        catch (Exception ex)
        {
            await RemoteLog.LogErrorAsync(ex, "UserIntegrationsPanel.LoadUsers");
        }
    }

    protected async Task ShowAddDialog()
    {
        NewIntegration = new CreateUserIntegrationRequest { IsActive = true, IsDefault = false };
        SelectedIntegrationType = "";
        SelectedAuthType = "";
        SelectedProviderId = "";
        SelectedUserId = "";
        DialogMessage = null;
        ShowDialog = true;
    }

    protected void CloseDialog()
    {
        ShowDialog = false;
        NewIntegration = new();
        SelectedIntegrationType = "";
        SelectedAuthType = "";
        SelectedProviderId = "";
        SelectedUserId = "";
    }

    protected void OnIntegrationTypeChanged()
    {
        if (Enum.TryParse<IntegrationType>(SelectedIntegrationType, out var type))
        {
            NewIntegration.IntegrationType = type;

            NewIntegration.AuthType = type switch
            {
                IntegrationType.CodeRepository => IntegrationAuthType.ApiToken,
                IntegrationType.CloudStorage => IntegrationAuthType.ApiKeySecret,
                _ => IntegrationAuthType.ApiToken
            };
            SelectedAuthType = NewIntegration.AuthType.ToString();
        }
    }

    protected async Task CreateIntegration()
    {
        if (IsAdmin && string.IsNullOrEmpty(SelectedUserId))
        {
            DialogMessage = "管理员必须选择目标用户";
            DialogSuccess = false;
            return;
        }
        
        if (string.IsNullOrEmpty(NewIntegration.DisplayName) ||
            string.IsNullOrEmpty(NewIntegration.Credential))
        {
            DialogMessage = "请填写必要字段";
            DialogSuccess = false;
            return;
        }

        IsSubmitting = true;
        DialogMessage = null;
        StateHasChanged();

        try
        {
            if (Enum.TryParse<IntegrationAuthType>(SelectedAuthType, out var authType))
            {
                NewIntegration.AuthType = authType;
            }

            if (IsAdmin && Guid.TryParse(SelectedUserId, out var targetUserId))
            {
                NewIntegration.UserId = targetUserId;
            }

            await ApiService.CreateIntegrationAsync(NewIntegration);
            DialogSuccess = true;
            DialogMessage = "集成创建成功！";
            await Task.Delay(500);
            CloseDialog();
            await OnRefresh.InvokeAsync();
        }
        catch (Exception ex)
        {
            await RemoteLog.LogErrorAsync(ex, "UserIntegrationsPanel.CreateIntegration");
            DialogMessage = $"创建失败: {ex.Message}";
            DialogSuccess = false;
        }
        finally
        {
            IsSubmitting = false;
            StateHasChanged();
        }
    }

    protected void OpenEditDialog(UserIntegrationResponse integration)
    {
        IntegrationToEdit = integration;
        EditIntegration = new UpdateUserIntegrationRequest
        {
            DisplayName = integration.DisplayName,
            Endpoint = integration.Endpoint,
            IsActive = integration.IsActive,
            IsDefault = integration.IsDefault
        };
        EditDialogMessage = null;
        ShowEditDialog = true;
    }

    protected void CloseEditDialog()
    {
        ShowEditDialog = false;
        IntegrationToEdit = null;
        EditIntegration = new();
    }

    protected async Task UpdateIntegration()
    {
        if (IntegrationToEdit == null) return;

        IsSubmitting = true;
        EditDialogMessage = null;
        StateHasChanged();

        try
        {
            await ApiService.UpdateIntegrationAsync(IntegrationToEdit.Id, EditIntegration);
            EditDialogSuccess = true;
            EditDialogMessage = "集成更新成功！";
            await Task.Delay(500);
            CloseEditDialog();
            await OnRefresh.InvokeAsync();
        }
        catch (Exception ex)
        {
            await RemoteLog.LogErrorAsync(ex, "UserIntegrationsPanel.UpdateIntegration");
            EditDialogMessage = $"更新失败: {ex.Message}";
            EditDialogSuccess = false;
        }
        finally
        {
            IsSubmitting = false;
            StateHasChanged();
        }
    }

    protected void OpenDeleteConfirm(UserIntegrationResponse integration)
    {
        IntegrationToDelete = integration;
        DeleteConfirmMessage = $"确定要删除集成 \"{integration.DisplayName}\" 吗？此操作无法撤销";
        ShowDeleteConfirm = true;
    }

    protected async Task ConfirmDelete()
    {
        if (IntegrationToDelete == null) return;

        try
        {
            var integrationId = IntegrationToDelete switch
            {
                UserIntegrationDetailedResponse detailedIntegration => detailedIntegration.Id,
                UserIntegrationResponse userIntegration => userIntegration.Id,
                _ => Guid.Empty
            };

            if (integrationId == Guid.Empty)
            {
                await RemoteLog.LogWarningAsync("Invalid integration type for deletion", "UserIntegrationsPanel.ConfirmDelete");
                return;
            }

            await ApiService.DeleteIntegrationAsync(integrationId);
            await OnRefresh.InvokeAsync();
        }
        catch (Exception ex)
        {
            var integrationId = IntegrationToDelete switch
            {
                UserIntegrationDetailedResponse detailedIntegration => detailedIntegration.Id,
                UserIntegrationResponse userIntegration => userIntegration.Id,
                _ => Guid.Empty
            };
            await RemoteLog.LogErrorAsync(ex, "UserIntegrationsPanel.ConfirmDelete", new() { ["IntegrationId"] = integrationId });
        }
        finally
        {
            IntegrationToDelete = null;
            ShowDeleteConfirm = false;
        }
    }

    // Event handlers for child views
    protected void HandleEditDetailed(UserIntegrationDetailedResponse integration)
    {
        // 需要将 UserIntegrationDetailedResponse 转换为 UserIntegrationResponse
        // 或者直接使用详细信息打开编辑对话框
        IntegrationToEdit = new UserIntegrationResponse
        {
            Id = integration.Id,
            DisplayName = integration.DisplayName,
            IntegrationType = integration.IntegrationType,
            IntegrationTypeName = integration.IntegrationTypeName,
            Endpoint = integration.Endpoint,
            IsActive = integration.IsActive,
            IsDefault = integration.IsDefault,
            ValidationStatus = integration.ValidationStatus,
            LastValidatedAt = integration.LastValidatedAt,
            CreatedAt = integration.CreatedAt,
            LastUsedAt = integration.LastUsedAt,
            UsageCount = integration.UsageCount
        };
        OpenEditDialog(IntegrationToEdit);
    }

    protected void HandleDeleteDetailedClick(UserIntegrationDetailedResponse integration)
    {
        IntegrationToDelete = integration;
        ShowDeleteConfirm = true;
        DeleteConfirmMessage = $"确定要删除 {integration.Username} 的集成 \"{integration.DisplayName}\" 吗?";
    }

    protected async Task HandleSetDefaultUser(UserIntegrationResponse integration)
    {
        try
        {
            await ApiService.SetAsDefaultIntegrationAsync(integration.Id);
            await OnRefresh.InvokeAsync();
        }
        catch (Exception ex)
        {
            await RemoteLog.LogErrorAsync(ex, "UserIntegrationsPanel.HandleSetDefaultUser");
        }
    }

    protected void HandleEditUser(UserIntegrationResponse integration)
    {
        OpenEditDialog(integration);
    }

    protected void HandleDeleteUser(UserIntegrationResponse integration)
    {
        IntegrationToDelete = integration;
        ShowDeleteConfirm = true;
        DeleteConfirmMessage = $"确定要删除集成 \"{integration.DisplayName}\" 吗?";
    }

    // Display name helpers
    protected static string GetIntegrationTypeDisplayName(IntegrationType type) => type switch
    {
        IntegrationType.CodeRepository => "代码仓库",
        IntegrationType.CloudStorage => "云存储",
        IntegrationType.ProjectManagement => "项目管理",
        IntegrationType.Communication => "通讯工具",
        IntegrationType.Calendar => "日历",
        IntegrationType.Email => "邮箱",
        IntegrationType.CICD => "CI/CD",
        IntegrationType.Monitoring => "监控",
        IntegrationType.Custom => "自定义",
        _ => type.ToString()
    };

    protected static string GetAuthTypeDisplayName(IntegrationAuthType authType) => authType switch
    {
        IntegrationAuthType.ApiToken => "API Token",
        IntegrationAuthType.OAuth2 => "OAuth 2.0",
        IntegrationAuthType.ApiKeySecret => "API Key + Secret",
        IntegrationAuthType.UsernamePassword => "用户名密码",
        IntegrationAuthType.JWT => "JWT",
        IntegrationAuthType.SSHKey => "SSH Key",
        _ => authType.ToString()
    };

    protected string GetCredentialLabel() => Enum.TryParse<IntegrationAuthType>(SelectedAuthType, out var authType)
        ? authType switch
        {
            IntegrationAuthType.ApiToken => "API Token",
            IntegrationAuthType.OAuth2 => "Access Token",
            IntegrationAuthType.ApiKeySecret => "API Key",
            IntegrationAuthType.UsernamePassword => "用户名",
            IntegrationAuthType.JWT => "JWT Token",
            IntegrationAuthType.SSHKey => "SSH Private Key",
            _ => "凭证"
        }
        : "凭证";

    protected string GetCredentialPlaceholder() => Enum.TryParse<IntegrationAuthType>(SelectedAuthType, out var authType)
        ? authType switch
        {
            IntegrationAuthType.ApiToken => "输入 API Token",
            IntegrationAuthType.OAuth2 => "输入 Access Token",
            IntegrationAuthType.ApiKeySecret => "输入 API Key",
            IntegrationAuthType.UsernamePassword => "输入用户名",
            IntegrationAuthType.JWT => "输入 JWT Token",
            IntegrationAuthType.SSHKey => "粘贴 SSH 私钥",
            _ => "输入凭证"
        }
        : "输入凭证";

    protected bool RequiresSecondaryCredential() =>
        Enum.TryParse<IntegrationAuthType>(SelectedAuthType, out var authType) &&
        authType is IntegrationAuthType.ApiKeySecret or IntegrationAuthType.UsernamePassword;

    protected string GetSecondaryCredentialLabel() => Enum.TryParse<IntegrationAuthType>(SelectedAuthType, out var authType)
        ? authType switch
        {
            IntegrationAuthType.ApiKeySecret => "API Secret",
            IntegrationAuthType.UsernamePassword => "密码",
            _ => "次要凭证"
        }
        : "次要凭证";

    protected string GetSecondaryCredentialPlaceholder() => Enum.TryParse<IntegrationAuthType>(SelectedAuthType, out var authType)
        ? authType switch
        {
            IntegrationAuthType.ApiKeySecret => "输入 API Secret",
            IntegrationAuthType.UsernamePassword => "输入密码",
            _ => "输入次要凭证"
        }
        : "输入次要凭证";
}
