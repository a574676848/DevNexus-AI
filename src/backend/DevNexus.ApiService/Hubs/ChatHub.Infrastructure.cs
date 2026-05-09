using Microsoft.AspNetCore.SignalR;

namespace DevNexus.ApiService.Hubs;

public partial class ChatHub
{
    /// <summary>
    /// 获取当前认证用户的ID
    /// </summary>
    private Guid GetCurrentUserId()
    {
        if (!_userContextAccessor.CurrentUserId.HasValue)
        {
            throw new HubException("用户未认证");
        }

        return _userContextAccessor.CurrentUserId.Value;
    }
}
