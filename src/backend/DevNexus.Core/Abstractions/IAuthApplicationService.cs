using DevNexus.Shared.DTOs.Auth;

namespace DevNexus.Core.Abstractions;

/// <summary>
/// Use-case oriented auth entrypoint for API/controllers.
/// </summary>
public interface IAuthApplicationService
{
    Task<TokenResponse?> LoginAsync(
        LoginRequest request,
        string? ipAddress = null,
        string? userAgent = null,
        CancellationToken cancellationToken = default);

    Task<TokenResponse?> RefreshTokenAsync(
        RefreshTokenRequest request,
        string? ipAddress = null,
        CancellationToken cancellationToken = default);

    Task<AuthResult> LogoutAsync(
        Guid currentUserId,
        RefreshTokenRequest request,
        CancellationToken cancellationToken = default);

    Task LogoutAllDevicesAsync(Guid currentUserId, CancellationToken cancellationToken = default);

    Task<AuthResult> ChangePasswordAsync(
        Guid currentUserId,
        ChangePasswordRequest request,
        CancellationToken cancellationToken = default);

    Task<UserInfo?> GetCurrentUserAsync(Guid currentUserId, CancellationToken cancellationToken = default);

    Task<AuthResult> UpdateProfileAsync(
        Guid currentUserId,
        UpdateProfileRequest request,
        CancellationToken cancellationToken = default);
}
