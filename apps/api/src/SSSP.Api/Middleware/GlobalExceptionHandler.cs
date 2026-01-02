<<<<<<< HEAD
﻿using System.Net;
using System.Text.Json;
using Microsoft.ApplicationInsights;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using static SSSP.Api.Middleware.CorrelationIdMiddleware;
=======
﻿using System;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.ApplicationInsights;
>>>>>>> main

namespace SSSP.Api.Middleware
{
    public sealed class GlobalExceptionHandler
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<GlobalExceptionHandler> _logger;
        private readonly TelemetryClient _telemetry;

        public GlobalExceptionHandler(
            RequestDelegate next,
            ILogger<GlobalExceptionHandler> logger,
            TelemetryClient telemetry)
        {
            _next = next;
            _logger = logger;
            _telemetry = telemetry;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (Exception ex)
            {
<<<<<<< HEAD
                await HandleAsync(context, ex);
            }
        }

        private async Task HandleAsync(HttpContext context, Exception ex)
        {
            var correlationId =
                context.Items.TryGetValue(ItemName, out var cidObj) && cidObj is string cid && !string.IsNullOrWhiteSpace(cid)
                    ? cid
                    : (context.TraceIdentifier ?? Guid.NewGuid().ToString("N"));

            var traceId = context.TraceIdentifier;

            var (status, errorCode, title) = Map(ex);

            _logger.LogError(ex,
                "Unhandled exception. CorrelationId={CorrelationId}, TraceId={TraceId}, Status={Status}, ErrorCode={ErrorCode}, Path={Path}, Method={Method}, ExceptionType={ExceptionType}",
                correlationId, traceId, (int)status, errorCode, context.Request.Path, context.Request.Method, ex.GetType().Name);

            _telemetry.TrackException(ex, new Dictionary<string, string>
            {
                ["CorrelationId"] = correlationId,
                ["TraceId"] = traceId ?? "N/A",
                ["Path"] = context.Request.Path,
                ["Method"] = context.Request.Method,
                ["StatusCode"] = ((int)status).ToString(),
                ["ErrorCode"] = errorCode
            });

            var problem = new ProblemDetails
            {
                Status = (int)status,
                Title = title,
                Detail = SafeDetail(ex, status),
                Instance = context.Request.Path
            };

            problem.Extensions["errorCode"] = errorCode;
            problem.Extensions["correlationId"] = correlationId;
            problem.Extensions["traceId"] = traceId;

            context.Response.StatusCode = (int)status;
            context.Response.ContentType = "application/problem+json";

            await context.Response.WriteAsync(JsonSerializer.Serialize(problem, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = false
            }));
        }

        private static string SafeDetail(Exception ex, HttpStatusCode status)
        {
            // big-company default: hide internal detail for 500 in prod
            if (status == HttpStatusCode.InternalServerError)
                return "An unexpected error occurred.";

            return ex.Message;
        }

        private static (HttpStatusCode Status, string ErrorCode, string Title) Map(Exception ex)
        {
            return ex switch
            {
                // 404
                KeyNotFoundException => (HttpStatusCode.NotFound, "not_found", "Resource not found"),

                // 400
                ArgumentNullException => (HttpStatusCode.BadRequest, "argument_null", "Missing required parameter"),
                ArgumentException => (HttpStatusCode.BadRequest, "invalid_argument", "Invalid parameter value"),

                // 401/403
                UnauthorizedAccessException => (HttpStatusCode.Forbidden, "forbidden", "Forbidden"),

                // 409 (state machine / concurrency)
                InvalidOperationException => (HttpStatusCode.Conflict, "conflict", "Operation not allowed in current state"),
                DbUpdateConcurrencyException => (HttpStatusCode.Conflict, "concurrency_conflict", "Concurrency conflict"),

                // 503 (optional: timeouts)
                TimeoutException => (HttpStatusCode.ServiceUnavailable, "timeout", "Service temporarily unavailable"),

                // 500
                _ => (HttpStatusCode.InternalServerError, "internal_error", "Unexpected error")
            };
        }
    }
}
=======
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

            // Track in Application Insights
            _telemetry.TrackException(exception, new Dictionary<string, string>
            {
                ["CorrelationId"] = correlationId,
                ["Path"] = context.Request.Path,
                ["Method"] = context.Request.Method,
                ["StatusCode"] = GetStatusCode(exception).ToString()
            });

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
>>>>>>> main
