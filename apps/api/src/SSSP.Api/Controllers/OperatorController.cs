using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SSSP.DAL.Context;
using SSSP.DAL.Models;
using System.Collections.Generic;
using System.Threading.Tasks;
using System;

namespace SSSP.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class OperatorController : ControllerBase
    {
        private readonly AppDbContext _context;

        public OperatorController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<Operator>>> GetAll()
        {
            var operators = await _context.Operators
                .Include(o => o.Users)
                .Include(o => o.Incidents)
                .Include(o => o.Cameras)
                .Include(o => o.Sensors)
                .ToListAsync();

            return Ok(operators);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<Operator>> GetById(int id)
        {
            var op = await _context.Operators
                .Include(o => o.Users)
                .Include(o => o.Incidents)
                .Include(o => o.Cameras)
                .Include(o => o.Sensors)
                .FirstOrDefaultAsync(o => o.Id == id);

            if (op == null)
                return NotFound($"Operator with ID {id} not found.");

            return Ok(op);
        }

        [HttpPost]
        public async Task<ActionResult<Operator>> Create([FromBody] Operator op)
        {
            op.CreatedAt = DateTime.UtcNow;

            _context.Operators.Add(op);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetById), new { id = op.Id }, op);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, [FromBody] Operator updated)
        {
            var existing = await _context.Operators.FindAsync(id);
            if (existing == null)
                return NotFound($"Operator with ID {id} not found.");

            existing.Name = updated.Name;
            existing.Type = updated.Type;
            existing.Location = updated.Location;
            existing.IsActive = updated.IsActive;
            //existing.UpdatedAt = DateTime.UtcNow;

            _context.Entry(existing).State = EntityState.Modified;
            await _context.SaveChangesAsync();

            return NoContent();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var op = await _context.Operators.FindAsync(id);
            if (op == null)
                return NotFound($"Operator with ID {id} not found.");

            _context.Operators.Remove(op);
            await _context.SaveChangesAsync();

            return NoContent();
        }
    }
}

