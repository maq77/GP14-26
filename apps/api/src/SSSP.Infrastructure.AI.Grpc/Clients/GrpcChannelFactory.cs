using System;
using System.Collections.Concurrent;
using System.Net.Http;
using System.Net.Security;
using System.Security.Authentication;
using Grpc.Net.Client;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SSSP.Infrastructure.AI.Grpc.Config;

namespace SSSP.Infrastructure.AI.Grpc.Clients
{
    public sealed class GrpcChannelFactory : IDisposable
    {
        private readonly AIOptions _options;
        private readonly ILogger<GrpcChannelFactory> _logger;
        private readonly ConcurrentDictionary<string, GrpcChannel> _channels = new();
        private bool _disposed;

        public GrpcChannelFactory(
            IOptions<AIOptions> options,
            ILogger<GrpcChannelFactory> logger)
        {
            _options = options.Value ?? throw new ArgumentNullException(nameof(options));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            if (string.IsNullOrWhiteSpace(_options.GrpcUrl))
                throw new InvalidOperationException("AI:GrpcUrl is not configured.");
        }

        public GrpcChannel CreateAiChannel(string? overrideAddress = null)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(GrpcChannelFactory));

            var address = string.IsNullOrWhiteSpace(overrideAddress)
                ? _options.GrpcUrl
                : overrideAddress;

            return _channels.GetOrAdd(address, BuildChannel);
        }

        private GrpcChannel BuildChannel(string address)
        {
            _logger.LogInformation(
                "Creating gRPC channel to {Address}",
                address);

            var handler = new SocketsHttpHandler
            {
                PooledConnectionIdleTimeout = TimeSpan.FromMinutes(5),
                PooledConnectionLifetime = TimeSpan.FromMinutes(30),
                EnableMultipleHttp2Connections = true,
                SslOptions =
                {
                    EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13,
                    RemoteCertificateValidationCallback = static (_, _, _, errors) =>
                    {
                        // In production: validate properly.
                        return errors == SslPolicyErrors.None;
                    }
                }
            };

            var channel = GrpcChannel.ForAddress(address, new GrpcChannelOptions
            {
                HttpHandler = handler
            });

            _logger.LogInformation(
                "gRPC channel created successfully for {Address}",
                address);

            return channel;
        }

        public int ChannelCount => _channels.Count;

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            foreach (var kvp in _channels)
            {
                try
                {
                    _logger.LogInformation(
                        "Disposing gRPC channel for {Address}",
                        kvp.Key);

                    kvp.Value.Dispose();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(
                        ex,
                        "Error disposing gRPC channel for {Address}",
                        kvp.Key);
                }
            }

            _channels.Clear();
        }
    }
}
