<<<<<<< HEAD
﻿using System.Diagnostics;
using Microsoft.ApplicationInsights;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using static SSSP.Api.Middleware.CorrelationIdMiddleware;
=======
﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.ApplicationInsights;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
>>>>>>> main

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
<<<<<<< HEAD
            _next = next;
            _logger = logger;
            _telemetry = telemetry;
=======
            _next = next ?? throw new ArgumentNullException(nameof(next));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _telemetry = telemetry ?? throw new ArgumentNullException(nameof(telemetry));
>>>>>>> main
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var sw = Stopwatch.StartNew();
<<<<<<< HEAD
=======
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
>>>>>>> main

            try
            {
                await _next(context);
            }
            finally
            {
                sw.Stop();
<<<<<<< HEAD
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
=======
                elapsedMs = sw.ElapsedMilliseconds;

                var statusCode = context.Response.StatusCode;
                var success = statusCode < 500; 
>>>>>>> main

                _telemetry.TrackMetric(
                    "RequestDurationMs",
                    elapsedMs,
                    new Dictionary<string, string>
                    {
                        ["Method"] = method,
                        ["Path"] = path,
                        ["StatusCode"] = statusCode.ToString(),
<<<<<<< HEAD
                        ["Success"] = success.ToString(),
                        ["CorrelationId"] = correlationId
=======
                        ["Success"] = success.ToString()
>>>>>>> main
                    });

                if (elapsedMs > SLOW_REQUEST_THRESHOLD_MS)
                {
                    _telemetry.TrackEvent("SlowRequest", new Dictionary<string, string>
                    {
                        ["Method"] = method,
                        ["Path"] = path,
                        ["StatusCode"] = statusCode.ToString(),
<<<<<<< HEAD
                        ["ElapsedMs"] = elapsedMs.ToString(),
                        ["CorrelationId"] = correlationId
                    });

                    _logger.LogWarning(
                        "SLOW REQUEST. Method={Method}, Path={Path}, StatusCode={StatusCode}, ElapsedMs={ElapsedMs}, CorrelationId={CorrelationId}",
                        method, path, statusCode, elapsedMs, correlationId);
=======
                        ["ElapsedMs"] = elapsedMs.ToString()
                    });
                }

                if (elapsedMs > SLOW_REQUEST_THRESHOLD_MS)
                {
                    _logger.LogWarning(
                        "SLOW REQUEST. Method={Method}, Path={Path}, StatusCode={StatusCode}, ElapsedMs={ElapsedMs}",
                        method, path, statusCode, elapsedMs);
>>>>>>> main
                }
                else
                {
                    _logger.LogDebug(
<<<<<<< HEAD
                        "Request completed. Method={Method}, Path={Path}, StatusCode={StatusCode}, ElapsedMs={ElapsedMs}, CorrelationId={CorrelationId}",
                        method, path, statusCode, elapsedMs, correlationId);
=======
                        "Request completed. Method={Method}, Path={Path}, StatusCode={StatusCode}, ElapsedMs={ElapsedMs}",
                        method, path, statusCode, elapsedMs);
>>>>>>> main
                }
            }
        }
    }
}
