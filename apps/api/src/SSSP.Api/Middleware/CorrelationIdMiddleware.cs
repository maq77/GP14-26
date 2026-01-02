using System.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace SSSP.Api.Middleware
{
    public sealed class CorrelationIdMiddleware
    {
        public const string HeaderName = "X-Correlation-Id";
<<<<<<< HEAD
        public const string ItemName = "CorrelationId";
=======
>>>>>>> main

        private readonly RequestDelegate _next;
        private readonly ILogger<CorrelationIdMiddleware> _logger;

<<<<<<< HEAD
        public CorrelationIdMiddleware(RequestDelegate next, ILogger<CorrelationIdMiddleware> logger)
=======
        public CorrelationIdMiddleware(
            RequestDelegate next,
            ILogger<CorrelationIdMiddleware> logger)
>>>>>>> main
        {
            _next = next;
            _logger = logger;
        }

        public async Task Invoke(HttpContext context)
        {
<<<<<<< HEAD
            var correlationId = context.Request.Headers.TryGetValue(HeaderName, out var cid) && !string.IsNullOrWhiteSpace(cid)
                ? cid.ToString()
                : Guid.NewGuid().ToString("N");

            // store in Items with stable key
            context.Items[ItemName] = correlationId;

            // return header
=======
            var correlationId = context.Request.Headers.TryGetValue(HeaderName, out var cid)
                ? cid.ToString()
                : Guid.NewGuid().ToString("N");

            context.Items[HeaderName] = correlationId;
>>>>>>> main
            context.Response.Headers[HeaderName] = correlationId;

            using (_logger.BeginScope(new Dictionary<string, object?>
            {
<<<<<<< HEAD
                [ItemName] = correlationId,
                ["TraceId"] = Activity.Current?.TraceId.ToString() ?? context.TraceIdentifier
=======
                ["CorrelationId"] = correlationId,
                ["TraceId"] = Activity.Current?.TraceId.ToString()
>>>>>>> main
            }))
            {
                await _next(context);
            }
        }
    }
}
