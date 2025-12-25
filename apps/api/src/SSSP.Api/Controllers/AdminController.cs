using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using SSSP.BL.DTOs.User;
using SSSP.BL.Services.Interfaces;

namespace SSSP.Api.Controllers
{
    //[Authorize(Roles = "Admin")]
    [ApiController]
    [Route("api/[controller]")]
    public class AdminController : ControllerBase
    {
        private readonly IUserService _userService;
        private readonly IRoleService _roleService;
        private readonly ILogger<AdminController> _logger;

        public AdminController(
            IUserService userService,
            IRoleService roleService,
            ILogger<AdminController> logger)
        {
            _userService = userService;
            _roleService = roleService;
            _logger = logger;
        }

        [HttpGet("users")]
        public async Task<IActionResult> GetAllUsers(CancellationToken ct)
        {
            var users = await _userService.GetAllAsync(ct);
            return Ok(users);
        }

        [HttpGet("roles")]
        public async Task<IActionResult> GetAllRoles(CancellationToken ct)
        {
            var roles = await _roleService.GetAllAsync(ct);
            return Ok(roles);
        }

        [HttpPost("create-role")]
        public async Task<IActionResult> CreateRole([FromQuery] string roleName, CancellationToken ct)
        {
            var result = await _roleService.CreateRoleAsync(roleName, ct);
            if (!result.Succeeded)
                return BadRequest(result.Error);

            return Ok(new { Message = $"Role '{roleName}' created" });
        }

        [HttpPost("assign-role")]
        public async Task<IActionResult> AssignRole([FromQuery] string userEmail, [FromQuery] string roleName, CancellationToken ct)
        {
            var result = await _roleService.AssignRoleAsync(userEmail, roleName, ct);
            if (!result.Succeeded)
                return BadRequest(result.Error);

            return Ok(new { Message = $"Role '{roleName}' assigned to '{userEmail}'" });
        }

        [HttpPost("remove-role")]
        public async Task<IActionResult> RemoveRole([FromQuery] string userEmail, [FromQuery] string roleName, CancellationToken ct)
        {
            var result = await _roleService.RemoveRoleAsync(userEmail, roleName, ct);
            if (!result.Succeeded)
                return BadRequest(result.Error);

            return Ok(new { Message = $"Role '{roleName}' removed from '{userEmail}'" });
        }

        [HttpDelete("delete-user")]
        public async Task<IActionResult> DeleteUser([FromQuery] string userEmail, CancellationToken ct)
        {
            var result = await _roleService.DeleteUserAsync(userEmail, ct);
            if (!result.Succeeded)
                return NotFound(result.Error);

            return Ok(new { Message = $"User '{userEmail}' deleted" });
        }

        [HttpPost("users")]
        public async Task<IActionResult> CreateUserWithRole([FromBody] CreateUserWithRoleDTO dto, CancellationToken ct)
        {
            var result = await _userService.CreateWithRoleAsync(dto, ct);
            if (!result.Succeeded)
                return BadRequest(result.Error);

            return CreatedAtAction(
                nameof(GetAllUsers),
                new { id = result.User!.Id },
                result.User);
        }

    }
}
