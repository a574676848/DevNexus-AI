using DevNexus.Client.Shared.Abstractions;
using DevNexus.Client.Shared.Services.Exceptions;
using DevNexus.Shared.DTOs.Auth;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

namespace DevNexus.Client.Shared.Components.Pages.Settings;

public partial class UserManagement : ComponentBase, IDisposable
{
    [Inject]
    private IApiService ApiService { get; set; } = null!;

    [Inject]
    private IRemoteLogService RemoteLog { get; set; } = null!;

    [Inject]
    private NavigationManager Navigation { get; set; } = null!;

    [Inject]
    private IJSRuntime JS { get; set; } = null!;

    private List<UserListItem> users = new();
    private int currentPage = 1;
    private int totalPages = 1;
    private int pageSize = 20;
    private string searchQuery = string.Empty;

    private bool isLoading = true;
    private bool isSaving;
    private string? message;
    private bool messageSuccess;

    // 对话框状态
    private bool showDialog;
    private bool isEditing;
    private bool showResetPasswordDialog;
    private bool showDeleteDialog;
    private UserListItem? selectedUser;
    private string? dialogError;
    private string newPassword = string.Empty;
    private CancellationTokenSource? _messageCts;
    private long _loadRequestVersion;

    private UserFormData formData = new();

    protected override async Task OnInitializedAsync()
    {
        await LoadUsers();
    }

    private async Task LoadUsers()
    {
        var requestVersion = Interlocked.Increment(ref _loadRequestVersion);
        isLoading = true;
        StateHasChanged();

        try
        {
            var response = await ApiService.GetUsersAsync(
                currentPage,
                pageSize,
                string.IsNullOrWhiteSpace(searchQuery) ? null : searchQuery);

            if (requestVersion != _loadRequestVersion)
            {
                return;
            }

            users = response.Items;
            totalPages = response.TotalPages > 0 ? response.TotalPages : 1;
        }
        catch (ApiException ex) when (ex.IsAuthenticationError || ex.IsAuthorizationError)
        {
            if (requestVersion != _loadRequestVersion)
            {
                return;
            }

            ShowMessage(ex.GetUserFriendlyMessage(), false);

            if (ex.IsAuthenticationError)
            {
                Navigation.NavigateTo("/login", forceLoad: true);
            }
        }
        catch (Exception ex)
        {
            if (requestVersion != _loadRequestVersion)
            {
                return;
            }

            await RemoteLog.LogErrorAsync(ex, "UserManagement.LoadUsers");
            ShowMessage("加载用户列表失败", false);
        }
        finally
        {
            if (requestVersion == _loadRequestVersion)
            {
                isLoading = false;
                StateHasChanged();
            }
        }
    }

    private async Task SearchUsers()
    {
        if (isLoading || isSaving)
        {
            return;
        }

        currentPage = 1;
        await LoadUsers();
    }

    private async Task HandleSearchKeyUp(KeyboardEventArgs e)
    {
        if (e.Key == "Enter")
        {
            await SearchUsers();
        }
    }

    private async Task PreviousPage()
    {
        if (isLoading || isSaving || currentPage <= 1)
        {
            return;
        }

        currentPage--;
        await LoadUsers();
    }

    private async Task NextPage()
    {
        if (isLoading || isSaving || currentPage >= totalPages)
        {
            return;
        }

        currentPage++;
        await LoadUsers();
    }

    private void ShowCreateDialog()
    {
        isEditing = false;
        formData = new UserFormData();
        dialogError = null;
        showDialog = true;
    }

    private void ShowEditDialog(UserListItem user)
    {
        isEditing = true;
        selectedUser = user;
        formData = new UserFormData
        {
            Username = user.Username,
            Email = user.Email,
            DisplayName = user.DisplayName,
            SelectedRole = user.Roles.FirstOrDefault() ?? "User"
        };
        dialogError = null;
        showDialog = true;
    }

    private void CloseDialog()
    {
        showDialog = false;
        selectedUser = null;
        dialogError = null;
    }

    private async Task SaveUser()
    {
        if (isSaving)
        {
            return;
        }

        dialogError = null;

        if (string.IsNullOrWhiteSpace(formData.Username) || string.IsNullOrWhiteSpace(formData.Email))
        {
            dialogError = "用户名和邮箱不能为空";
            return;
        }

        if (!isEditing && string.IsNullOrWhiteSpace(formData.Password))
        {
            dialogError = "密码不能为空";
            return;
        }

        isSaving = true;
        StateHasChanged();

        try
        {
            AuthResult result;
            if (isEditing && selectedUser != null)
            {
                result = await ApiService.UpdateUserAsync(
                    selectedUser.Id,
                    new UpdateUserRequest
                    {
                        Email = formData.Email,
                        DisplayName = formData.DisplayName,
                        Roles = new List<string> { formData.SelectedRole }
                    });
            }
            else
            {
                result = await ApiService.CreateUserAsync(
                    new CreateUserRequest
                    {
                        Username = formData.Username,
                        Email = formData.Email,
                        Password = formData.Password,
                        DisplayName = formData.DisplayName,
                        Roles = new List<string> { formData.SelectedRole }
                    });
            }

            if (result.Succeeded)
            {
                ShowMessage(isEditing ? "用户更新成功" : "用户创建成功", true);
                CloseDialog();
                await LoadUsers();
            }
            else
            {
                dialogError = result.Errors.FirstOrDefault() ?? result.Message ?? "操作失败";
            }
        }
        catch (ApiException ex) when (ex.IsAuthenticationError || ex.IsAuthorizationError)
        {
            dialogError = ex.GetUserFriendlyMessage();
            if (ex.IsAuthenticationError)
            {
                Navigation.NavigateTo("/login", forceLoad: true);
            }
        }
        catch (Exception ex)
        {
            await RemoteLog.LogErrorAsync(ex, "UserManagement.CreateUser");
            dialogError = "保存失败，请重试";
        }
        finally
        {
            isSaving = false;
            StateHasChanged();
        }
    }

    private void ShowResetPasswordDialog(UserListItem user)
    {
        selectedUser = user;
        newPassword = string.Empty;
        dialogError = null;
        showResetPasswordDialog = true;
    }

    private void CloseResetPasswordDialog()
    {
        showResetPasswordDialog = false;
        selectedUser = null;
        newPassword = string.Empty;
        dialogError = null;
    }

    private async Task ResetPassword()
    {
        if (isSaving || selectedUser == null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(newPassword) || newPassword.Length < 6)
        {
            dialogError = "密码长度不能少于6位";
            return;
        }

        isSaving = true;
        StateHasChanged();

        try
        {
            var result = await ApiService.AdminResetPasswordAsync(selectedUser.Id, newPassword);
            if (result.Succeeded)
            {
                ShowMessage("密码重置成功", true);
                CloseResetPasswordDialog();
            }
            else
            {
                dialogError = result.Errors.FirstOrDefault() ?? "密码重置失败";
            }
        }
        catch (ApiException ex) when (ex.IsAuthenticationError || ex.IsAuthorizationError)
        {
            dialogError = ex.GetUserFriendlyMessage();
            if (ex.IsAuthenticationError)
            {
                Navigation.NavigateTo("/login", forceLoad: true);
            }
        }
        catch (Exception ex)
        {
            await RemoteLog.LogErrorAsync(ex, "UserManagement.UpdatePassword");
            dialogError = "重置失败，请重试";
        }
        finally
        {
            isSaving = false;
            StateHasChanged();
        }
    }

    private void ShowDeleteDialog(UserListItem user)
    {
        selectedUser = user;
        dialogError = null;
        showDeleteDialog = true;
    }

    private void CloseDeleteDialog()
    {
        showDeleteDialog = false;
        selectedUser = null;
        dialogError = null;
    }

    private async Task DeleteUser()
    {
        if (isSaving || selectedUser == null)
        {
            return;
        }

        isSaving = true;
        StateHasChanged();

        try
        {
            var result = await ApiService.DeleteUserAsync(selectedUser.Id);
            if (result.Succeeded)
            {
                ShowMessage("用户删除成功", true);
                CloseDeleteDialog();
                await LoadUsers();
            }
            else
            {
                dialogError = result.Errors.FirstOrDefault() ?? "删除失败";
            }
        }
        catch (ApiException ex) when (ex.IsAuthenticationError || ex.IsAuthorizationError)
        {
            dialogError = ex.GetUserFriendlyMessage();
            if (ex.IsAuthenticationError)
            {
                Navigation.NavigateTo("/login", forceLoad: true);
            }
        }
        catch (Exception ex)
        {
            await RemoteLog.LogErrorAsync(ex, "UserManagement.DeleteUser", new() { ["UserId"] = selectedUser.Id });
            dialogError = "删除失败，请重试";
        }
        finally
        {
            isSaving = false;
            StateHasChanged();
        }
    }

    private async Task ToggleUserStatus(UserListItem user)
    {
        if (isSaving)
        {
            return;
        }

        isSaving = true;
        StateHasChanged();

        try
        {
            var result = await ApiService.ToggleUserStatusAsync(user.Id);
            if (result.Succeeded)
            {
                ShowMessage(result.Message ?? "状态切换成功", true);
                await LoadUsers();
            }
            else
            {
                ShowMessage(result.Errors.FirstOrDefault() ?? "状态切换失败", false);
            }
        }
        catch (ApiException ex) when (ex.IsAuthenticationError || ex.IsAuthorizationError)
        {
            ShowMessage(ex.GetUserFriendlyMessage(), false);
            if (ex.IsAuthenticationError)
            {
                Navigation.NavigateTo("/login", forceLoad: true);
            }
        }
        catch (Exception ex)
        {
            await RemoteLog.LogErrorAsync(ex, "UserManagement.ToggleStatus");
            ShowMessage("操作失败，请重试", false);
        }
        finally
        {
            isSaving = false;
            await InvokeAsync(StateHasChanged);
        }
    }

    private void ShowMessage(string msg, bool success)
    {
        _messageCts?.Cancel();
        _messageCts?.Dispose();
        _messageCts = new CancellationTokenSource();

        message = msg;
        messageSuccess = success;
        StateHasChanged();

        _ = HideMessageAsync(_messageCts.Token);
    }

    private async Task HideMessageAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(3000, cancellationToken);
            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            message = null;
            await InvokeAsync(StateHasChanged);
        }
        catch (OperationCanceledException)
        {
        }
    }

    /// <summary>
    /// 获取角色显示名称
    /// </summary>
    private static string GetRoleDisplayName(string role)
    {
        return role.ToLower() switch
        {
            "admin" => "管理员",
            "user" => "普通用户",
            _ => role
        };
    }

    private sealed class UserFormData
    {
        public string Username { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string SelectedRole { get; set; } = "User";
    }

    public void Dispose()
    {
        _messageCts?.Cancel();
        _messageCts?.Dispose();
    }
}
