using Microsoft.AspNetCore.SignalR;

namespace FriendMap.Api.Hubs;

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

    public async Task SendMessage(string threadId, string senderId, string body)
    {
        await Clients.Group(threadId).SendAsync("ReceiveMessage", new
        {
            ThreadId = threadId,
            SenderId = senderId,
            Body = body,
            SentAt = DateTimeOffset.UtcNow
        });
    }
}
