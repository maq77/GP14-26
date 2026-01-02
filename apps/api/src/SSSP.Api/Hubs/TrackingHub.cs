<<<<<<< HEAD
﻿using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace SSSP.Api.Hubs;

//[Authorize] 
public sealed class TrackingHub : Hub
{
    public const string HubUrl = "/hubs/tracking";

    public override async Task OnConnectedAsync()
    {
        var userIdStr = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);

        if (Guid.TryParse(userIdStr, out var userId))
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"user:{userId}");
        }

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userIdStr = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);

        if (Guid.TryParse(userIdStr, out var userId))
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"user:{userId}");
        }

        await base.OnDisconnectedAsync(exception);
    }
}
=======
﻿using Microsoft.AspNetCore.SignalR;
using System.Threading.Tasks;


namespace SSSP.Api.Hubs
{
    public class TrackingHub : Hub
    {
        public const string HubUrl = "/hubs/tracking";


        public async Task Subscribe(string cameraId)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, cameraId);
        }


        public async Task Unsubscribe(string cameraId)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, cameraId);
        }
    }
}
>>>>>>> main
