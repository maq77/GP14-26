using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using SSSP.Api.Middleware;

namespace SSSP.Api.Extentions
{
    public static class PerformanceMonitoringMiddlewareExtensions
    {
        public static IApplicationBuilder UsePerformanceMonitoring(this IApplicationBuilder app)
        {
            return app.UseMiddleware<PerformanceMonitoringMiddleware>();
        }
    }
}