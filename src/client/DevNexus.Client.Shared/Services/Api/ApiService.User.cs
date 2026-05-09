using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using DevNexus.Shared.DTOs.Auth;
using Microsoft.Extensions.Logging;
using DevNexus.Client.Shared.Services.Exceptions;
namespace DevNexus.Client.Shared.Services.Api;

/// <summary>
/// REST API 服务 - 用户管理部分
/// </summary>
public partial class ApiService
{
    #region 用户管理

    /// <inheritdoc />
    public async Task<UserInfo> GetCurrentUserAsync()
    {
        var response = await _httpClient.GetAsync("/api/v1/auth/me");
        await EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<UserInfo>()
            ?? throw new ApiException("获取用户信息失败");
    }

    /// <inheritdoc />
    public async Task<AuthResult> ChangePasswordAsync(ChangePasswordRequest request)
    {
        var response = await _httpClient.PostAsJsonAsync("/api/v1/auth/change-password", request);
        
        // 修改密码时需要特殊处理 400 响应，因为它包含有意义的错误信息
        if (response.StatusCode == HttpStatusCode.BadRequest)
        {
            var errorResult = await response.Content.ReadFromJsonAsync<AuthResult>();
            if (errorResult != null)
            {
                return errorResult;
            }
            return new AuthResult 
            { 
                Succeeded = false, 
                Errors = new List<string> { "密码修改失败" } 
            };
        }
        
        await EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<AuthResult>()
            ?? new AuthResult { Succeeded = true, Message = "密码修改成功" };
    }

    /// <inheritdoc />
    public async Task LogoutAllDevicesAsync()
    {
        var response = await _httpClient.PostAsync("/api/v1/auth/logout-all", null);
        await EnsureSuccessAsync(response);
    }

    /// <inheritdoc />
    public async Task<AuthResult> UpdateProfileAsync(UpdateProfileRequest request)
    {
        var response = await _httpClient.PutAsJsonAsync("/api/v1/auth/profile", request);
        await EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<AuthResult>()
            ?? new AuthResult { Succeeded = true };
    }

    #endregion

    #region 用户管理 (管理员)

    /// <inheritdoc />
    public async Task<UserListResponse> GetUsersAsync(int page = 1, int pageSize = 20, string? search = null)
    {
        var query = $"?page={page}&pageSize={pageSize}";
        if (!string.IsNullOrWhiteSpace(search))
            query += $"&search={Uri.EscapeDataString(search)}";

        var response = await _httpClient.GetAsync($"/api/v1/user{query}");
        await EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<UserListResponse>() ?? new();
    }

    /// <inheritdoc />
    public async Task<UserInfo?> GetUserByIdAsync(Guid userId)
    {
        var response = await _httpClient.GetAsync($"/api/v1/user/{userId}");
        if (!response.IsSuccessStatusCode)
            return null;
        return await response.Content.ReadFromJsonAsync<UserInfo>();
    }

    /// <inheritdoc />
    public async Task<AuthResult> CreateUserAsync(CreateUserRequest request)
    {
        var response = await _httpClient.PostAsJsonAsync("/api/v1/user", request);
        await EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<AuthResult>()
            ?? new AuthResult { Succeeded = true, Message = "用户创建成功" };
    }

    /// <inheritdoc />
    public async Task<AuthResult> UpdateUserAsync(Guid userId, UpdateUserRequest request)
    {
        var response = await _httpClient.PutAsJsonAsync($"/api/v1/user/{userId}", request);
        await EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<AuthResult>()
            ?? new AuthResult { Succeeded = true, Message = "用户更新成功" };
    }

    /// <inheritdoc />
    public async Task<AuthResult> DeleteUserAsync(Guid userId)
    {
        var response = await _httpClient.DeleteAsync($"/api/v1/user/{userId}");
        await EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<AuthResult>()
            ?? new AuthResult { Succeeded = true, Message = "用户删除成功" };
    }

    /// <inheritdoc />
    public async Task<AuthResult> AdminResetPasswordAsync(Guid userId, string newPassword)
    {
        var response = await _httpClient.PostAsJsonAsync($"/api/v1/user/{userId}/reset-password", new { newPassword });
        await EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<AuthResult>()
            ?? new AuthResult { Succeeded = true, Message = "密码重置成功" };
    }

    /// <inheritdoc />
    public async Task<AuthResult> ToggleUserStatusAsync(Guid userId)
    {
        var response = await _httpClient.PutAsync($"/api/v1/user/{userId}/toggle-status", null);
        await EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<AuthResult>()
            ?? new AuthResult { Succeeded = true };
    }

    #endregion
}

