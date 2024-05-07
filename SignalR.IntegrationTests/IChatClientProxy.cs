namespace SignalR.IntegrationTests;

public interface IChatClientProxy
{
    public Task ReceiveMessage(string message);
}
