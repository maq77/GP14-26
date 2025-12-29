using System.Diagnostics;
using Microsoft.ApplicationInsights;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using static SSSP.Api.Middleware.CorrelationIdMiddleware;

namespace SSSP.Api.Middleware
{
    public sealed class PerformanceMonitoringMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<PerformanceMonitoringMiddleware> _logger;
        private readonly TelemetryClient _telemetry;
        private const int SLOW_REQUEST_THRESHOLD_MS = 1000;

        public PerformanceMonitoringMiddleware(
            RequestDelegate next,
            ILogger<PerformanceMonitoringMiddleware> logger,
            TelemetryClient telemetry)
        {
            _next = next;
            _logger = logger;
            _telemetry = telemetry;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var sw = Stopwatch.StartNew();

            try
            {
                await _next(context);
            }
            finally
            {
                sw.Stop();
                var elapsedMs = sw.ElapsedMilliseconds;

                // response header after calculation
                if (!context.Response.HasStarted)
                    context.Response.Headers["X-Response-Time-Ms"] = elapsedMs.ToString();

                var path = context.Request.Path.Value ?? string.Empty;
                var method = context.Request.Method;
                var statusCode = context.Response.StatusCode;
                var success = statusCode < 500;

                var correlationId =
                    context.Items.TryGetValue(ItemName, out var cidObj) && cidObj is string cid && !string.IsNullOrWhiteSpace(cid)
                        ? cid
                        : "N/A";

                _telemetry.TrackMetric(
                    "RequestDurationMs",
                    elapsedMs,
                    new Dictionary<string, string>
                    {
                        ["Method"] = method,
                        ["Path"] = path,
                        ["StatusCode"] = statusCode.ToString(),
                        ["Success"] = success.ToString(),
                        ["CorrelationId"] = correlationId
                    });

                if (elapsedMs > SLOW_REQUEST_THRESHOLD_MS)
                {
                    _telemetry.TrackEvent("SlowRequest", new Dictionary<string, string>
                    {
                        ["Method"] = method,
                        ["Path"] = path,
                        ["StatusCode"] = statusCode.ToString(),
                        ["ElapsedMs"] = elapsedMs.ToString(),
                        ["CorrelationId"] = correlationId
                    });

                    _logger.LogWarning(
                        "SLOW REQUEST. Method={Method}, Path={Path}, StatusCode={StatusCode}, ElapsedMs={ElapsedMs}, CorrelationId={CorrelationId}",
                        method, path, statusCode, elapsedMs, correlationId);
                }
                else
                {
                    _logger.LogDebug(
                        "Request completed. Method={Method}, Path={Path}, StatusCode={StatusCode}, ElapsedMs={ElapsedMs}, CorrelationId={CorrelationId}",
                        method, path, statusCode, elapsedMs, correlationId);
                }
            }
        }
    }
}
