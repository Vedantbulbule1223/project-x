using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace SwiftRoute.API.Hubs;

[Authorize]
public class PulseHub : Hub
{
    private readonly ILogger<PulseHub> _logger;
    public PulseHub(ILogger<PulseHub> logger) => _logger = logger;

    public override async Task OnConnectedAsync()
    {
        var user = Context.User?.Identity?.Name ?? "Unknown";
        _logger.LogInformation("Client connected: {User} ({ConnectionId})", user, Context.ConnectionId);
        await Clients.Caller.SendAsync("Connected", $"Welcome to Pulse Command Center, {user}!");
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogInformation("Client disconnected: {ConnectionId}", Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }

    // Client can subscribe to a specific vehicle's updates
    public async Task SubscribeToVehicle(string vin)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"vehicle_{vin}");
    }

    public async Task UnsubscribeFromVehicle(string vin)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"vehicle_{vin}");
    }
}
