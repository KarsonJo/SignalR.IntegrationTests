using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace SignalR.IntegrationTests;

[Authorize]
public class ChatHub(ChatService _chatService) : Hub<IChatClientProxy>
{
    public async Task SendMessage(string message)
    {
        await _chatService.SendMessageToAllAsync(message);
    }
}
