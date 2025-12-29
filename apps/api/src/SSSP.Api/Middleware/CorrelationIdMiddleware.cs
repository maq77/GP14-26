using System.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace SSSP.Api.Middleware
{
    public sealed class CorrelationIdMiddleware
    {
        public const string HeaderName = "X-Correlation-Id";
        public const string ItemName = "CorrelationId";

        private readonly RequestDelegate _next;
        private readonly ILogger<CorrelationIdMiddleware> _logger;

        public CorrelationIdMiddleware(RequestDelegate next, ILogger<CorrelationIdMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task Invoke(HttpContext context)
        {
            var correlationId = context.Request.Headers.TryGetValue(HeaderName, out var cid) && !string.IsNullOrWhiteSpace(cid)
                ? cid.ToString()
                : Guid.NewGuid().ToString("N");

            // store in Items with stable key
            context.Items[ItemName] = correlationId;

            // return header
            context.Response.Headers[HeaderName] = correlationId;

            using (_logger.BeginScope(new Dictionary<string, object?>
            {
                [ItemName] = correlationId,
                ["TraceId"] = Activity.Current?.TraceId.ToString() ?? context.TraceIdentifier
            }))
            {
                await _next(context);
            }
        }
    }
}
