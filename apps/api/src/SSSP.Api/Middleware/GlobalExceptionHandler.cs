using System;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace SSSP.Api.Middleware
{
    public sealed class GlobalExceptionHandler
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<GlobalExceptionHandler> _logger;

        public GlobalExceptionHandler(
            RequestDelegate next,
            ILogger<GlobalExceptionHandler> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (Exception ex)
            {
                await HandleExceptionAsync(context, ex);
            }
        }

        private async Task HandleExceptionAsync(HttpContext context, Exception exception)
        {
            var correlationId = Guid.NewGuid().ToString();

            _logger.LogError(
                exception,
                "Unhandled exception. CorrelationId={CorrelationId}, Path={Path}, Method={Method}, ExceptionType={ExceptionType}",
                correlationId,
                context.Request.Path,
                context.Request.Method,
                exception.GetType().Name);

            var response = new ErrorResponse
            {
                CorrelationId = correlationId,
                Message = GetUserFriendlyMessage(exception),
                Type = exception.GetType().Name,
                Timestamp = DateTimeOffset.UtcNow
            };

            var statusCode = GetStatusCode(exception);

            context.Response.ContentType = "application/json";
            context.Response.StatusCode = (int)statusCode;

            var json = JsonSerializer.Serialize(response, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = false
            });

            await context.Response.WriteAsync(json);
        }

        private static string GetUserFriendlyMessage(Exception exception)
        {
            return exception switch
            {
                ArgumentNullException => "Required parameter is missing",
                ArgumentException => "Invalid parameter value",
                InvalidOperationException => "The operation cannot be completed at this time",
                UnauthorizedAccessException => "You are not authorized to perform this action",
                _ => "An unexpected error occurred. Please contact support if the problem persists."
            };
        }

        private static HttpStatusCode GetStatusCode(Exception exception)
        {
            return exception switch
            {
                ArgumentNullException => HttpStatusCode.BadRequest,
                ArgumentException => HttpStatusCode.BadRequest,
                InvalidOperationException => HttpStatusCode.BadRequest,
                UnauthorizedAccessException => HttpStatusCode.Unauthorized,
                _ => HttpStatusCode.InternalServerError
            };
        }

        private sealed record ErrorResponse
        {
            public string CorrelationId { get; init; } = string.Empty;
            public string Message { get; init; } = string.Empty;
            public string Type { get; init; } = string.Empty;
            public DateTimeOffset Timestamp { get; init; }
        }
    }

}