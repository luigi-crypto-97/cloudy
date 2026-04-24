using Microsoft.AspNetCore.SignalR.Client;

namespace FriendMap.Mobile.Services;

public class ChatHubService
{
    private HubConnection? _connection;
    private readonly ApiClient _apiClient;

    public event EventHandler<HubMessageArgs>? MessageReceived;

    public ChatHubService(ApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    public async Task ConnectAsync()
    {
        if (_connection is not null) return;

        var baseUrl = _apiClient.BaseAddress?.ToString().TrimEnd('/');
        if (string.IsNullOrWhiteSpace(baseUrl)) return;

        var token = await SecureStorage.GetAsync("friendmap_access_token");
        _connection = new HubConnectionBuilder()
            .WithUrl($"{baseUrl}/hubs/chat", options =>
            {
                options.AccessTokenProvider = () => Task.FromResult<string?>(token);
            })
            .WithAutomaticReconnect()
            .Build();

        _connection.On<string, string, string>("ReceiveMessage", (threadId, senderId, body) =>
        {
            MessageReceived?.Invoke(this, new HubMessageArgs(threadId, senderId, body));
        });

        await _connection.StartAsync();
    }

    public async Task JoinThreadAsync(string threadId)
    {
        if (_connection?.State == HubConnectionState.Connected)
            await _connection.InvokeAsync("JoinThread", threadId);
    }

    public async Task LeaveThreadAsync(string threadId)
    {
        if (_connection?.State == HubConnectionState.Connected)
            await _connection.InvokeAsync("LeaveThread", threadId);
    }

    public async Task DisconnectAsync()
    {
        if (_connection is not null)
        {
            await _connection.StopAsync();
            _connection = null;
        }
    }
}

public class HubMessageArgs : EventArgs
{
    public string ThreadId { get; }
    public string SenderId { get; }
    public string Body { get; }

    public HubMessageArgs(string threadId, string senderId, string body)
    {
        ThreadId = threadId;
        SenderId = senderId;
        Body = body;
    }
}
