using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace SSSP.Api.Middleware
{
    public sealed class PerformanceMonitoringMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<PerformanceMonitoringMiddleware> _logger;
        private const int SLOW_REQUEST_THRESHOLD_MS = 1000;

        public PerformanceMonitoringMiddleware(
            RequestDelegate next,
            ILogger<PerformanceMonitoringMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var sw = Stopwatch.StartNew();
            var path = context.Request.Path.Value ?? string.Empty;
            var method = context.Request.Method;

            try
            {
                await _next(context);
            }
            finally
            {
                sw.Stop();

                var statusCode = context.Response.StatusCode;
                var elapsedMs = sw.ElapsedMilliseconds;

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

                context.Response.Headers.Append("X-Response-Time-Ms", elapsedMs.ToString());
            }
        }
    }

}