using System;
using System.Threading;
using System.Threading.Tasks;
using Grpc.Core;
using Grpc.Net.Client;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SSSP.Infrastructure.AI.Grpc.Config;
using Sssp.Ai.Face;

namespace SSSP.Infrastructure.AI.Grpc.Health
{
    public sealed class AiGrpcHealthCheck : IHealthCheck
    {
        private readonly AIOptions _options;
        private readonly ILogger<AiGrpcHealthCheck> _logger;

        public AiGrpcHealthCheck(
            IOptions<AIOptions> options,
            ILogger<AiGrpcHealthCheck> logger)
        {
            _options = options.Value;
            _logger = logger;
        }

        public async Task<HealthCheckResult> CheckHealthAsync(
            HealthCheckContext context,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(_options.GrpcUrl))
            {
                return HealthCheckResult.Unhealthy("AI:GrpcUrl is not configured.");
            }

            try
            {
                using var channel = GrpcChannel.ForAddress(_options.GrpcUrl);
                var client = new FaceService.FaceServiceClient(channel);

                var reply = await client.GetModelInfoAsync(
                    new FaceModelInfoRequest(),
                    deadline: DateTime.UtcNow.AddSeconds(2),
                    cancellationToken: cancellationToken);

                var data = new
                {
                    reply.ModelName,
                    reply.ModelVersion,
                    reply.Device,
                    reply.EmbeddingDim,
                    reply.TotalFacesEnrolled
                };

                _logger.LogInformation(
                    "AI gRPC health OK. Model={ModelName} Version={Version} Device={Device} TotalFaces={TotalFaces}",
                    reply.ModelName,
                    reply.ModelVersion,
                    reply.Device,
                    reply.TotalFacesEnrolled);

                return HealthCheckResult.Healthy(
                    description: "AI gRPC reachable and model info retrieved.",
                    data: new System.Collections.Generic.Dictionary<string, object?>
                    {
                        ["model"] = reply.ModelName,
                        ["version"] = reply.ModelVersion,
                        ["device"] = reply.Device,
                        ["embedding_dim"] = reply.EmbeddingDim,
                        ["total_faces"] = reply.TotalFacesEnrolled
                    });
            }
            catch (RpcException ex)
            {
                _logger.LogError(
                    ex,
                    "AI gRPC health check failed. Status={Status}",
                    ex.StatusCode);

                return HealthCheckResult.Unhealthy(
                    description: $"AI gRPC RPC error: {ex.StatusCode}");
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "AI gRPC health check failed with unexpected error.");

                return HealthCheckResult.Unhealthy(
                    description: "AI gRPC unexpected error.");
            }
        }
    }
}
