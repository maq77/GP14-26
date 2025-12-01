using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.ApplicationInsights;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

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
            _next = next ?? throw new ArgumentNullException(nameof(next));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _telemetry = telemetry ?? throw new ArgumentNullException(nameof(telemetry));
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var sw = Stopwatch.StartNew();
            var path = context.Request.Path.Value ?? string.Empty;
            var method = context.Request.Method;
            long elapsedMs = 0;

            context.Response.OnStarting(() =>
            {
                if (!context.Response.HasStarted)
                {
                    if (!context.Response.Headers.ContainsKey("X-Response-Time-Ms"))
                    {
                        context.Response.Headers.Append("X-Response-Time-Ms", elapsedMs.ToString());
                    }
                }

                return Task.CompletedTask;
            });

            try
            {
                await _next(context);
            }
            finally
            {
                sw.Stop();
                elapsedMs = sw.ElapsedMilliseconds;

                var statusCode = context.Response.StatusCode;
                var success = statusCode < 500; 

                _telemetry.TrackMetric(
                    "RequestDurationMs",
                    elapsedMs,
                    new Dictionary<string, string>
                    {
                        ["Method"] = method,
                        ["Path"] = path,
                        ["StatusCode"] = statusCode.ToString(),
                        ["Success"] = success.ToString()
                    });

                if (elapsedMs > SLOW_REQUEST_THRESHOLD_MS)
                {
                    _telemetry.TrackEvent("SlowRequest", new Dictionary<string, string>
                    {
                        ["Method"] = method,
                        ["Path"] = path,
                        ["StatusCode"] = statusCode.ToString(),
                        ["ElapsedMs"] = elapsedMs.ToString()
                    });
                }

                if (elapsedMs > SLOW_REQUEST_THRESHOLD_MS)
                {
                    _logger.LogWarning(
                        "SLOW REQUEST. Method={Method}, Path={Path}, StatusCode={StatusCode}, ElapsedMs={ElapsedMs}",
                        method, path, statusCode, elapsedMs);
                }
                else
                {
                    _logger.LogDebug(
                        "Request completed. Method={Method}, Path={Path}, StatusCode={StatusCode}, ElapsedMs={ElapsedMs}",
                        method, path, statusCode, elapsedMs);
                }
            }
        }
    }
}
