using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SSSP.BL.DTOs.User;

namespace SSSP.BL.Services.Interfaces
{
    public interface IUserService
    {
        Task<IReadOnlyList<UserDTO>> GetAllAsync(CancellationToken ct);
        Task<UserDTO?> GetByIdAsync(Guid id, CancellationToken ct);
        Task<(bool Succeeded, string? Error, UserDTO? User)> CreateWithRoleAsync(CreateUserWithRoleDTO dto, CancellationToken ct);
        Task<(bool Succeeded, string? Error)> UpdateAsync(Guid id, UpdateUserDTO dto, CancellationToken ct);
        Task<(bool Succeeded, string? Error)> DeleteAsync(Guid id, CancellationToken ct);
    }
}
