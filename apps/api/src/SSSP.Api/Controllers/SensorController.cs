using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using SSSP.BL.Services.Interfaces;
using SSSP.DAL.Models;

namespace SSSP.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class SensorController : ControllerBase
    {
        private readonly ISensorService _sensorService;
        private readonly ILogger<SensorController> _logger;

        public SensorController(ISensorService sensorService, ILogger<SensorController> logger)
        {
            _sensorService = sensorService;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll(CancellationToken ct)
        {
            var sensors = await _sensorService.GetAllAsync(ct);
            return Ok(sensors);
        }

        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetById(int id, CancellationToken ct)
        {
            var sensor = await _sensorService.GetByIdAsync(id, ct);
            if (sensor == null)
                return NotFound();

            return Ok(sensor);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] Sensor sensor, CancellationToken ct)
        {
            var created = await _sensorService.CreateAsync(sensor, ct);
            return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
        }

        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update(int id, [FromBody] Sensor sensor, CancellationToken ct)
        {
            var ok = await _sensorService.UpdateAsync(id, sensor, ct);
            if (!ok)
                return NotFound();

            return NoContent();
        }

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id, CancellationToken ct)
        {
            var ok = await _sensorService.DeleteAsync(id, ct);
            if (!ok)
                return NotFound();

            return NoContent();
        }
    }
}
