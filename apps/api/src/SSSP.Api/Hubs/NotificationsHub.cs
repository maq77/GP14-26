using Microsoft.AspNetCore.SignalR;
using SSSP.Api.Realtime;
using SSSP.Api.Realtime.Groups;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace SSSP.Api.Hubs;

// [Authorize] // enable in prod
public sealed class NotificationsHub : Hub<INotificationsClient>
{
    public const string HubUrl = "/hubs/notifications";

    public override async Task OnConnectedAsync()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, RtGroups.Global);

        var sub = Context.User?.FindFirstValue(JwtRegisteredClaimNames.Sub)
                  ?? Context.User?.FindFirstValue("sub")
                  ?? Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);

        if (Guid.TryParse(sub, out var userId))
            await Groups.AddToGroupAsync(Context.ConnectionId, RtGroups.User(userId));

        foreach (var role in Context.User?.FindAll(ClaimTypes.Role) ?? Enumerable.Empty<Claim>())
        {
            if (!string.IsNullOrWhiteSpace(role.Value))
                await Groups.AddToGroupAsync(Context.ConnectionId, RtGroups.Role(role.Value));
        }

        await base.OnConnectedAsync();
    }

    // subscriptions
    public Task JoinGlobal() => Groups.AddToGroupAsync(Context.ConnectionId, RtGroups.Global);
    public Task LeaveGlobal() => Groups.RemoveFromGroupAsync(Context.ConnectionId, RtGroups.Global);

    public Task SubscribeCamera(string cameraId) =>
        Groups.AddToGroupAsync(Context.ConnectionId, RtGroups.Camera(cameraId));

    public Task UnsubscribeCamera(string cameraId) =>
        Groups.RemoveFromGroupAsync(Context.ConnectionId, RtGroups.Camera(cameraId));

    public Task SubscribeOperator(int operatorId) =>
        Groups.AddToGroupAsync(Context.ConnectionId, RtGroups.Operator(operatorId));

    public Task UnsubscribeOperator(int operatorId) =>
        Groups.RemoveFromGroupAsync(Context.ConnectionId, RtGroups.Operator(operatorId));

    public Task SubscribeIncident(int incidentId) =>
        Groups.AddToGroupAsync(Context.ConnectionId, RtGroups.Incident(incidentId));

    public Task UnsubscribeIncident(int incidentId) =>
        Groups.RemoveFromGroupAsync(Context.ConnectionId, RtGroups.Incident(incidentId));
}
