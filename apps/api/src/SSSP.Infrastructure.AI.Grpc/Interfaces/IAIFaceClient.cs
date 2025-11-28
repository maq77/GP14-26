using System.Threading.Tasks;
using Sssp.Ai.Face;

namespace SSSP.Infrastructure.AI.Grpc.Interfaces
{
    public interface IAIFaceClient
    {
        Task<FaceVerifyResponse> VerifyFaceAsync(byte[] imageBytes, string cameraId);
        Task<FaceEmbeddingResponse> ExtractEmbeddingAsync(
            byte[] image,
            string? cameraId = null,
            CancellationToken cancellationToken = default);
    }
}
