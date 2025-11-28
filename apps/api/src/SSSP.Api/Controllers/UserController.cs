using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using SSSP.Api.DTO.UserDTO;
using SSSP.BL.DTOs.User;
using SSSP.BL.Services.Interfaces;

namespace SSSP.Api.Controllers
{
    //[Authorize(Roles = "Admin,Operator")]
    [ApiController]
    [Route("api/[controller]")]
    public class UserController : ControllerBase
    {
        private readonly IUserService _userService;
        private readonly ILogger<UserController> _logger;

        public UserController(IUserService userService, ILogger<UserController> logger)
        {
            _userService = userService;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> GetAllUsers(CancellationToken ct)
        {
            var users = await _userService.GetAllAsync(ct);
            return Ok(users);
        }

        [HttpGet("{id:guid}")]
        public async Task<IActionResult> GetUser(Guid id, CancellationToken ct)
        {
            var user = await _userService.GetByIdAsync(id, ct);
            if (user == null)
                return NotFound("User not found");

            return Ok(user);
        }

        [HttpPut("{id:guid}")]
        public async Task<IActionResult> UpdateUser(Guid id, [FromBody] UpdateUserDTO dto, CancellationToken ct)
        {
            var result = await _userService.UpdateAsync(id, dto, ct);
            if (!result.Succeeded)
                return BadRequest(result.Error);

            return Ok("User updated successfully");
        }

        [Authorize(Roles = "Admin")]
        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> DeleteUser(Guid id, CancellationToken ct)
        {
            var result = await _userService.DeleteAsync(id, ct);
            if (!result.Succeeded)
                return NotFound(result.Error);

            return Ok("User deleted successfully");
        }
    }
}
