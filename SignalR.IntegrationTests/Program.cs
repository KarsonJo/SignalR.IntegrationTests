using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using SignalR.IntegrationTests;

const string HubsPrefix = "/hubs";

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddAuthorizationBuilder()
    .SetDefaultPolicy(new AuthorizationPolicyBuilder()
        .AddAuthenticationSchemes([
            IdentityConstants.ApplicationScheme,
            IdentityConstants.BearerScheme
        ])
        .RequireAuthenticatedUser()
        .Build());
builder.Services.AddAuthentication()
    .AddCookie(IdentityConstants.ApplicationScheme)
    .AddBearerToken(IdentityConstants.BearerScheme, o =>
    {
        /* Add for SignalR Authentication:
         * https://learn.microsoft.com/en-us/aspnet/core/signalr/authn-and-authz?view=aspnetcore-8.0
         * https://www.reddit.com/r/dotnet/comments/17ebizo/signalr_hub_auth/
         */
        o.Events = new()
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                var path = context.HttpContext.Request.Path;
                if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments(HubsPrefix))
                {
                    context.Token = accessToken;
                }
                return Task.CompletedTask;
            }
        };
    });
builder.Services.AddIdentityCore<IdentityUser>()
    .AddApiEndpoints()
    .AddEntityFrameworkStores<IdentityDbContext>();
builder.Services.AddDbContext<IdentityDbContext>(x => x.UseInMemoryDatabase("db"));

builder.Services.AddSignalR();

builder.Services.AddScoped<ChatService>();

var app = builder.Build();

app.MapIdentityApi<IdentityUser>();

app.MapGroup(HubsPrefix).MapHub<ChatHub>("/chat");

app.Run();

public partial class Program { }