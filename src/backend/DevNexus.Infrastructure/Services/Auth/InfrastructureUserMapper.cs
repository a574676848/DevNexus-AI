using DevNexus.Core.Models;
using DevNexus.Infrastructure.Models;

namespace DevNexus.Infrastructure.Services.Auth;

internal static class InfrastructureUserMapper
{
    internal static UserIdentityModel ToIdentityModel(InfrastructureUser user)
    {
        return new UserIdentityModel
        {
            Id = user.Id,
            Username = user.UserName ?? string.Empty,
            Email = user.Email,
            EmailConfirmed = user.EmailConfirmed,
            PhoneNumber = user.PhoneNumber,
            DisplayName = user.DisplayName,
            AvatarUrl = user.AvatarUrl,
            IsEnabled = user.IsEnabled,
            LastLoginAt = user.LastLoginAt,
            LastLoginDeviceId = user.LastLoginDeviceId,
            CreatedAt = user.CreatedAt,
            UpdatedAt = user.UpdatedAt
        };
    }

    internal static InfrastructureUser ToInfrastructureUser(UserIdentityModel source)
    {
        var target = new InfrastructureUser();
        Apply(source, target);
        return target;
    }

    internal static void Apply(UserIdentityModel source, InfrastructureUser target)
    {
        target.Id = source.Id == Guid.Empty ? Guid.NewGuid() : source.Id;
        target.UserName = source.Username;
        target.Email = source.Email;
        target.EmailConfirmed = source.EmailConfirmed;
        target.PhoneNumber = source.PhoneNumber;
        target.DisplayName = source.DisplayName;
        target.AvatarUrl = source.AvatarUrl;
        target.IsEnabled = source.IsEnabled;
        target.LastLoginAt = source.LastLoginAt;
        target.LastLoginDeviceId = source.LastLoginDeviceId;
        target.CreatedAt = source.CreatedAt;
        target.UpdatedAt = source.UpdatedAt;
    }
}
