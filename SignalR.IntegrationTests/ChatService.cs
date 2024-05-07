using Microsoft.AspNetCore.SignalR;

namespace SignalR.IntegrationTests;

public class ChatService(IHubContext<ChatHub, IChatClientProxy> _hubContext)
{
    public async Task SendMessageToAllAsync(string message)
    {
        await _hubContext.Clients.All.ReceiveMessage(message);
    }
}
