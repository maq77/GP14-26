using System.Threading;
using System.Threading.Tasks;
using SSSP.Infrastructure.AI.Grpc.Video;
using Sssp.Ai.Stream;

namespace SSSP.Infrastructure.AI.Grpc.Interfaces
{
    public interface IVideoStreamClient
    {
        Task StreamCameraAsync(
            string cameraId,
            string rtspUrl,
            Func<VideoFrameResponse, Task> onFrameResponse,
            CancellationToken cancellationToken = default);
    }
}
