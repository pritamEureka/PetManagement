using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Pawzaroo.Api.Hubs;

[Authorize]
public class NotificationHub : Hub
{
    public override Task OnConnectedAsync()
    {
        if (Context.UserIdentifier is { } uid)
        {
            Groups.AddToGroupAsync(Context.ConnectionId, $"user:{uid}");
        }
        return base.OnConnectedAsync();
    }
}
