using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
using System.Data;
using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;

namespace SignalR.IntegrationTests.Tests;

public class WebAppFactory : WebApplicationFactory<Program> { }

public class HubIntegrationTests(WebAppFactory _factory) : IClassFixture<WebAppFactory>
{
    const string DefaultPassword = "Password1!";

    private async Task<string> GetUserAccessTokenAsync(string email, string password = DefaultPassword, bool autoCreate = true)
    {
        if (autoCreate)
        {
            // Ensure user exists.
            _ = await GetOrCreateUserAsync(email, password);
        }

        var client = _factory.CreateClient();

        object credentials = new
        {
            email = email!,
            password = password!,
        };
        HttpResponseMessage response = await client.PostAsJsonAsync("/login", credentials);

        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync();
        Debug.WriteLine(content);
        return JsonDocument.Parse(content).RootElement.GetProperty("accessToken").ToString();
    }

    private async Task<IdentityUser> GetOrCreateUserAsync(string email, string password = DefaultPassword)
    {
        using var scope = _factory.Services.CreateScope();

        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();

        var newUser = await userManager.FindByEmailAsync(email);

        // Create user if not exists.
        if (newUser == null)
        {
            newUser = new()
            {
                UserName = email,
                Email = email,
            };

            var createPowerUser = await userManager.CreateAsync(newUser, password);
            if (!createPowerUser.Succeeded)
            {
                throw new Exception(string.Join('\n', createPowerUser.Errors.Select(x => x.Description)));
            }
        }

        return newUser ?? throw new Exception("Failed to create user.");
    }

    private HubConnection SetupHubConnection(string path, string? token = null)
    {
        var server = _factory.Server;

        var uri = new Uri(server.BaseAddress, path);

        return new HubConnectionBuilder()
            .WithUrl(uri, o =>
            {
                // Force the use of WebSockets.
                o.Transports = HttpTransportType.WebSockets;
                // Support non-socket transports. (Can be omitted here.)
                o.HttpMessageHandlerFactory = _ => server.CreateHandler();
                // Support socket transports.
                o.WebSocketFactory = async (context, cancellationToken) =>
                {
                    var wsClient = server.CreateWebSocketClient();

                    if (token != null)
                    {
                        // Authentication for socket transports. (Chooses one of these.)
                        // Option1: Use request headers.
                        wsClient.ConfigureRequest = request => request.Headers.Authorization = new($"Bearer {token}");
                        // Option2: Add access token to query string.
                        uri = new Uri(QueryHelpers.AddQueryString(context.Uri.ToString(), "access_token", token));
                        // I like both ;)
                    }
                    else
                    {
                        uri = context.Uri;
                    }

                    return await wsClient.ConnectAsync(uri, cancellationToken);
                };
                o.SkipNegotiation = true;
                // Authentication for non-socket transports. (Can be omitted here.)
                o.AccessTokenProvider = () => Task.FromResult(token);
            })
            .Build();
    }

    [Fact]
    public async Task MessageTest()
    {
        // --> Arrange
        var token = await GetUserAccessTokenAsync("1@test.com");
        var connection = SetupHubConnection("/hubs/chat", token);

        string? received = null;
        connection.On<string>(nameof(IChatClientProxy.ReceiveMessage), (m) => received = m);

        await connection.StartAsync();

        string message = "Hello World";


        // --> Act
        await connection.InvokeAsync(nameof(ChatHub.SendMessage), message);
        // Wait for messages to be received. You may need to increase the delay if you're running in a slow environment.
        await Task.Delay(1);


        // --> Assert
        Assert.Equal(message, received);
    }

    [Fact]
    public async Task UnauthorizedAccessTest()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await SetupHubConnection("/hubs/chat", "123").StartAsync());
    }
}