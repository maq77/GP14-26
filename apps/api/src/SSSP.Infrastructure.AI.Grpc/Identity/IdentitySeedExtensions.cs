using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using SSSP.DAL.Enums;
using SSSP.DAL.Models;
using System;
using System.Threading.Tasks;

namespace SSSP.Infrastructure.Identity
{
    public static class IdentitySeedExtensions
    {
        public static async Task SeedRolesAsync(this IServiceProvider services)
        {
            using var scope = services.CreateScope();
            var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<Role>>();

            var roleNames = Enum.GetNames(typeof(UserRole));
            foreach (var roleName in roleNames)
            {
                var exists = await roleManager.RoleExistsAsync(roleName);
                if (!exists)
                {
                    var role = new Role
                    {
                        Id = Guid.NewGuid(),
                        Name = roleName,
                        NormalizedName = roleName.ToUpperInvariant()
                    };

                    await roleManager.CreateAsync(role);
                }
            }
        }
    }
}
