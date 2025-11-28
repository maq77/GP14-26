using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SSSP.BL.DTOs.User;
using SSSP.BL.Services.Interfaces;
using SSSP.DAL.Enums;
using SSSP.DAL.Models;

namespace SSSP.BL.Services
{
    public class UserService : IUserService
    {
        private readonly UserManager<User> _userManager;
        private readonly RoleManager<Role> _roleManager;
        private readonly ILogger<UserService> _logger;

        public UserService(
            UserManager<User> userManager,
            RoleManager<Role> roleManager,
            ILogger<UserService> logger)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _logger = logger;
        }

        public async Task<IReadOnlyList<UserDTO>> GetAllAsync(CancellationToken ct)
        {
            var users = await _userManager.Users.AsNoTracking().ToListAsync(ct);

            return users.Select(u => new UserDTO
            {
                Id = u.Id,
                FullName = u.FullName,
                Email = u.Email!,
                OperatorId = u.OperatorId,
                IsActive = u.IsActive,
                CreatedAt = u.CreatedAt ?? DateTime.MinValue
            }).ToList();
        }

        public async Task<UserDTO?> GetByIdAsync(Guid id, CancellationToken ct)
        {
            var user = await _userManager.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
            if (user == null)
                return null;

            return new UserDTO
            {
                Id = user.Id,
                FullName = user.FullName,
                Email = user.Email!,
                OperatorId = user.OperatorId,
                IsActive = user.IsActive,
                CreatedAt = user.CreatedAt ?? DateTime.MinValue
            };
        }

        public async Task<(bool Succeeded, string? Error, UserDTO? User)> CreateWithRoleAsync(
            CreateUserWithRoleDTO dto,
            CancellationToken ct)
        {
            var roleName = dto.Role.ToString();

            _logger.LogInformation("Creating user {Email} with role {Role}", dto.Email, roleName);

            var existing = await _userManager.FindByEmailAsync(dto.Email);
            if (existing != null)
                return (false, "Email already exists", null);

            if (!await _roleManager.RoleExistsAsync(roleName))
            {
                var createRole = await _roleManager.CreateAsync(new Role
                {
                    Id = Guid.NewGuid(),
                    Name = roleName,
                    NormalizedName = roleName.ToUpperInvariant()
                });

                if (!createRole.Succeeded)
                {
                    var errorCreateRole = string.Join("; ", createRole.Errors.Select(e => e.Description));
                    _logger.LogWarning("Failed to create role {Role}: {Error}", roleName, errorCreateRole);
                    return (false, errorCreateRole, null);
                }
            }

            var user = new User
            {
                Id = Guid.NewGuid(),
                Email = dto.Email,
                UserName = dto.Email,
                FullName = dto.FullName,
                OperatorId = dto.OperatorId,
                Role = dto.Role,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            var createResult = await _userManager.CreateAsync(user, dto.Password);
            if (!createResult.Succeeded)
            {
                var errorCreateUser = string.Join("; ", createResult.Errors.Select(e => e.Description));
                _logger.LogWarning("Failed to create user {Email}: {Error}", dto.Email, errorCreateUser);
                return (false, errorCreateUser, null);
            }

            var roleResult = await _userManager.AddToRoleAsync(user, roleName);
            if (!roleResult.Succeeded)
            {
                var errorAddRole = string.Join("; ", roleResult.Errors.Select(e => e.Description));
                _logger.LogWarning("Failed to assign role {Role} to {Email}: {Error}", roleName, dto.Email, errorAddRole);
                return (false, errorAddRole, null);
            }

            _logger.LogInformation("User {Email} created with role {Role}", dto.Email, roleName);

            var userDto = new UserDTO
            {
                Id = user.Id,
                FullName = user.FullName,
                Email = user.Email!,
                OperatorId = user.OperatorId,
                IsActive = user.IsActive,
                CreatedAt = user.CreatedAt ?? DateTime.MinValue
            };

            return (true, null, userDto);
        }

        public async Task<(bool Succeeded, string? Error)> UpdateAsync(Guid id, UpdateUserDTO dto, CancellationToken ct)
        {
            var user = await _userManager.FindByIdAsync(id.ToString());
            if (user == null)
                return (false, "User not found");

            if (!string.IsNullOrWhiteSpace(dto.FullName))
                user.FullName = dto.FullName;

            if (dto.OperatorId.HasValue)
                user.OperatorId = dto.OperatorId;

            if (dto.IsActive.HasValue)
                user.IsActive = dto.IsActive.Value;

            user.UpdatedAt = DateTime.UtcNow;

            var result = await _userManager.UpdateAsync(user);
            if (!result.Succeeded)
            {
                var error = string.Join("; ", result.Errors.Select(e => e.Description));
                _logger.LogWarning("Failed to update user {Id}: {Error}", id, error);
                return (false, error);
            }

            _logger.LogInformation("User {Id} updated", id);
            return (true, null);
        }

        public async Task<(bool Succeeded, string? Error)> DeleteAsync(Guid id, CancellationToken ct)
        {
            var user = await _userManager.FindByIdAsync(id.ToString());
            if (user == null)
                return (false, "User not found");

            var result = await _userManager.DeleteAsync(user);
            if (!result.Succeeded)
            {
                var error = string.Join("; ", result.Errors.Select(e => e.Description));
                _logger.LogWarning("Failed to delete user {Id}: {Error}", id, error);
                return (false, error);
            }

            _logger.LogInformation("User {Id} deleted", id);
            return (true, null);
        }
    }
}
