using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using SSSP.BL.DTOs.User;
using SSSP.BL.Services.Interfaces;
using SSSP.DAL.Models;

namespace SSSP.BL.Services
{
    public class RoleService : IRoleService
    {
        private readonly UserManager<User> _userManager;
        private readonly RoleManager<Role> _roleManager;
        private readonly ILogger<RoleService> _logger;

        public RoleService(
            UserManager<User> userManager,
            RoleManager<Role> roleManager,
            ILogger<RoleService> logger)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _logger = logger;
        }

        public async Task<IReadOnlyList<RoleDTO>> GetAllAsync(CancellationToken ct)
        {
            var roles = _roleManager.Roles.ToList();
            return roles.Select(r => new RoleDTO
            {
                Id = r.Id,
                Name = r.Name!
            }).ToList();
        }

        public async Task<(bool Succeeded, string? Error)> CreateRoleAsync(string roleName, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(roleName))
                return (false, "Role name is required");

            if (await _roleManager.RoleExistsAsync(roleName))
                return (false, "Role already exists");

            var role = new Role
            {
                Id = Guid.NewGuid(),
                Name = roleName,
                NormalizedName = roleName.ToUpperInvariant()
            };

            var result = await _roleManager.CreateAsync(role);
            if (!result.Succeeded)
            {
                var error = string.Join("; ", result.Errors.Select(e => e.Description));
                _logger.LogWarning("Failed to create role {Role}: {Error}", roleName, error);
                return (false, error);
            }

            _logger.LogInformation("Role {Role} created", roleName);
            return (true, null);
        }

        public async Task<(bool Succeeded, string? Error)> AssignRoleAsync(string userEmail, string roleName, CancellationToken ct)
        {
            var user = await _userManager.FindByEmailAsync(userEmail);
            if (user == null)
                return (false, "User not found");

            if (!await _roleManager.RoleExistsAsync(roleName))
                return (false, "Role not found");

            var result = await _userManager.AddToRoleAsync(user, roleName);
            if (!result.Succeeded)
            {
                var error = string.Join("; ", result.Errors.Select(e => e.Description));
                _logger.LogWarning("Failed to assign role {Role} to {Email}: {Error}", roleName, userEmail, error);
                return (false, error);
            }

            _logger.LogInformation("Role {Role} assigned to {Email}", roleName, userEmail);
            return (true, null);
        }

        public async Task<(bool Succeeded, string? Error)> RemoveRoleAsync(string userEmail, string roleName, CancellationToken ct)
        {
            var user = await _userManager.FindByEmailAsync(userEmail);
            if (user == null)
                return (false, "User not found");

            if (!await _roleManager.RoleExistsAsync(roleName))
                return (false, "Role not found");

            var result = await _userManager.RemoveFromRoleAsync(user, roleName);
            if (!result.Succeeded)
            {
                var error = string.Join("; ", result.Errors.Select(e => e.Description));
                _logger.LogWarning("Failed to remove role {Role} from {Email}: {Error}", roleName, userEmail, error);
                return (false, error);
            }

            _logger.LogInformation("Role {Role} removed from {Email}", roleName, userEmail);
            return (true, null);
        }

        public async Task<(bool Succeeded, string? Error)> DeleteUserAsync(string userEmail, CancellationToken ct)
        {
            var user = await _userManager.FindByEmailAsync(userEmail);
            if (user == null)
                return (false, "User not found");

            var result = await _userManager.DeleteAsync(user);
            if (!result.Succeeded)
            {
                var error = string.Join("; ", result.Errors.Select(e => e.Description));
                _logger.LogWarning("Failed to delete user {Email}: {Error}", userEmail, error);
                return (false, error);
            }

            _logger.LogInformation("User {Email} deleted", userEmail);
            return (true, null);
        }
    }
}
