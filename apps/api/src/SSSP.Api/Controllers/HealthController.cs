using System;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;

namespace SSSP.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class HealthController : ControllerBase
    {
        private readonly IHostEnvironment _env;

        public HealthController(IHostEnvironment env)
        {
            _env = env;
        }

        [HttpGet]
        public IActionResult Get()
        {
            return Ok(new
            {
                status = "healthy",
                timestamp = DateTime.UtcNow,
                service = "SSSP API",
                environment = _env.EnvironmentName
            });
        }
    }
}
