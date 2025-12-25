using Microsoft.AspNetCore.SignalR;
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