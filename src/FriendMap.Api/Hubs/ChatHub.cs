using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace FriendMap.Api.Hubs;

[Authorize]
public class ChatHub : Hub
{
    public async Task JoinThread(string threadId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, threadId);
    }

    public async Task LeaveThread(string threadId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, threadId);
    }

    public Task SendMessage(string threadId, string body)
    {
        var senderId = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? Context.User?.FindFirstValue("sub")
            ?? Context.UserIdentifier
            ?? string.Empty;

        return BroadcastMessage(threadId, senderId, body);
    }

    private async Task BroadcastMessage(string threadId, string senderId, string body)
    {
        if (string.IsNullOrWhiteSpace(threadId) || string.IsNullOrWhiteSpace(body))
        {
            return;
        }

        await Clients.Group(threadId).SendAsync("ReceiveMessage", new
        {
            threadId,
            senderId,
            body,
            sentAt = DateTimeOffset.UtcNow
        });
    }
}
