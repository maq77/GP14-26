using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Sssp.Ai.Face;
using SSSP.BL.Managers;
using SSSP.BL.Records;

namespace SSSP.BL.Services.Interfaces
{
    public interface IFaceRecognitionService
    {
        Task<FaceMatchResult> VerifyAsync(
            byte[] image,
            string cameraId,
            CancellationToken ct = default);

        Task<FaceMatchResult> VerifyEmbeddingAsync(
            IReadOnlyList<float> embedding,
            string cameraId,
            CancellationToken ct = default);
        Task<IReadOnlyList<FaceMatchResult>> VerifyEmbeddingsBatchAsync(
            IReadOnlyList<IReadOnlyList<float>> embeddings,
            string cameraId,
            CancellationToken ct = default);
        Task<IReadOnlyList<FaceRecognitionHit>> VerifyManyAsync(
            byte[] image,
            string cameraId,
            CancellationToken ct = default);

        Task<IReadOnlyList<FaceRecognitionHit>> VerifyManyFromEmbeddingsAsync(
            FaceEmbeddingResponse response,
            string cameraId,
            CancellationToken ct = default);
    }
}
