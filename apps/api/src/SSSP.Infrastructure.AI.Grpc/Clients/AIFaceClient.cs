using Google.Protobuf.WellKnownTypes;
using Grpc.Net.Client;
using Microsoft.Extensions.Configuration;
using static System.Net.Mime.MediaTypeNames;
using System.Diagnostics;
using Microsoft.Extensions.Options;

using SSSP.Infrastructure.AI.Grpc.Interfaces;
using SSSP.Infrastructure.AI.Grpc.Config;
using Sssp.Ai.Face;

public class AIFaceClient : IAIFaceClient
{
    private readonly AIOptions aiOptions;

    private readonly FaceService.FaceServiceClient _client;

    public AIFaceClient(IOptions<AIOptions> options)
    {
        aiOptions = options.Value;

        if (string.IsNullOrWhiteSpace(aiOptions.GrpcUrl))
            throw new InvalidOperationException("AI:GrpcUrl is not configured.");

        var channel = GrpcChannel.ForAddress(aiOptions.GrpcUrl);
        _client = new FaceService.FaceServiceClient(channel);
    }

    public async Task<FaceVerifyResponse> VerifyFaceAsync(byte[] imageBytes, string cameraId)
    {
        var request = new FaceVerifyRequest
        {
            Image = Google.Protobuf.ByteString.CopyFrom(imageBytes),
            CameraId = cameraId,
            CheckBlacklist = true
            // optionally:
            // ConfidenceThreshold = 0.7f
        };

        return await _client.VerifyFaceAsync(request);
    }

    public async Task<FaceEmbeddingResponse> ExtractEmbeddingAsync(
        byte[] image,
        string? cameraId = null,
        CancellationToken cancellationToken = default)
    {
        var req = new FaceEmbeddingRequest
        {
            Image = Google.Protobuf.ByteString.CopyFrom(image),
        };

        if (!string.IsNullOrWhiteSpace(cameraId))
            req.CameraId = cameraId;

        return await _client.ExtractEmbeddingAsync(req, cancellationToken: cancellationToken);
    }
}