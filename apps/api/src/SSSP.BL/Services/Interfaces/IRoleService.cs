using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SSSP.BL.DTOs.User;

namespace SSSP.BL.Services.Interfaces
{
    public interface IRoleService
    {
        Task<IReadOnlyList<RoleDTO>> GetAllAsync(CancellationToken ct);
        Task<(bool Succeeded, string? Error)> CreateRoleAsync(string roleName, CancellationToken ct);
        Task<(bool Succeeded, string? Error)> AssignRoleAsync(string userEmail, string roleName, CancellationToken ct);
        Task<(bool Succeeded, string? Error)> RemoveRoleAsync(string userEmail, string roleName, CancellationToken ct);
        Task<(bool Succeeded, string? Error)> DeleteUserAsync(string userEmail, CancellationToken ct);
    }
}
