using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using SSSP.Api.DTOs.Incidents;
using SSSP.BL.Services.Interfaces;
using SSSP.DAL.Models;

namespace SSSP.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public sealed class IncidentController : ControllerBase
    {
        private readonly IIncidentService _service;
        private readonly ILogger<IncidentController> _logger;

        public IncidentController(
            IIncidentService service,
            ILogger<IncidentController> logger)
        {
            _service = service;
            _logger = logger;
        }

        [HttpPost]
        public async Task<ActionResult<IncidentResponse>> Create(
            [FromBody] CreateIncidentRequest request,
            CancellationToken ct)
        {
            var idempotencyKey = Request.Headers.TryGetValue("Idempotency-Key", out var v)
                ? v.ToString()
                : null;

            var incident = await _service.CreateAsync(
                request.Title,
                request.Description,
                request.Type,
                request.Source,
                request.OperatorId,
                request.Location,
                request.PayloadJson,
                idempotencyKey,
                ct);

            return Ok(ToResponse(incident));
        }

        [HttpPost("{id:int}/start/{userId:guid}")]
        public async Task<IActionResult> StartWork(int id, Guid userId, CancellationToken ct)
        {
            await _service.StartWorkAsync(id, userId, ct);
            return Ok();
        }

        [HttpPost("{id:int}/resolve/{userId:guid}")]
        public async Task<IActionResult> Resolve(int id, Guid userId, CancellationToken ct)
        {
            await _service.ResolveAsync(id, userId, ct);
            return Ok();
        }

        [HttpPost("{id:int}/close")]
        public async Task<IActionResult> Close(int id, CancellationToken ct)
        {
            await _service.CloseAsync(id, ct);
            return Ok();
        }

        [HttpPost("{id:int}/assign/{userId:guid}")]
        public async Task<IActionResult> Assign(int id, Guid userId, CancellationToken ct)
        {
            await _service.AssignAsync(id, userId, ct);
            return Ok();
        }

        [HttpGet("open")]
        public async Task<ActionResult<IEnumerable<IncidentResponse>>> GetOpen(CancellationToken ct)
        {
            var incidents = await _service.GetOpenAsync(ct);
            return Ok(incidents.Select(ToResponse));
        }

        [HttpGet("operator/{operatorId:int}")]
        public async Task<ActionResult<IEnumerable<IncidentResponse>>> GetByOperator(
            int operatorId,
            CancellationToken ct)
        {
            var incidents = await _service.GetByOperatorAsync(operatorId, ct);
            return Ok(incidents.Select(ToResponse));
        }

        private static IncidentResponse ToResponse(Incident incident)
        {
            return new IncidentResponse
            {
                Id = incident.Id,
                Title = incident.Title,
                Description = incident.Description,
                Type = incident.Type,
                Severity = incident.Severity,
                Status = incident.Status,
                Source = incident.Source,
                OperatorId = incident.OperatorId,
                Location = incident.Location,
                AssignedToUserId = incident.AssignedToUserId,
                Timestamp = incident.Timestamp,
                ResolvedAt = incident.ResolvedAt
            };
        }
    }
}
