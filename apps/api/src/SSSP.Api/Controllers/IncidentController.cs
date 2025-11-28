using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SSSP.DAL.Context;
using SSSP.DAL.Models;
using System.Threading.Tasks;

namespace SSSP.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class IncidentController : ControllerBase
    {
        private readonly AppDbContext _context;

        public IncidentController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var incidents = await _context.Incidents.ToListAsync();
            return Ok(incidents);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var incident = await _context.Incidents.FindAsync(id);

            if (incident == null)
                return NotFound();

            return Ok(incident);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] Incident incident)
        {
            if (incident == null)
                return BadRequest();


            _context.Incidents.Add(incident);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetById), new { id = incident.Id }, incident);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, [FromBody] Incident model)
        {
            var incident = await _context.Incidents.FindAsync(id);

            if (incident == null)
                return NotFound();

            incident.Type = model.Type;
            incident.Status = model.Status;
            incident.Severity = model.Severity;
            incident.Source = model.Source;
            incident.Description = model.Description;
            incident.Timestamp = model.Timestamp;
            incident.Location = model.Location;

            await _context.SaveChangesAsync();

            return NoContent();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var incident = await _context.Incidents.FindAsync(id);

            if (incident == null)
                return NotFound();

            _context.Incidents.Remove(incident);
            await _context.SaveChangesAsync();

            return NoContent();
        }
    }
}
