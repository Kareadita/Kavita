using System.Collections.Generic;
using System.Threading.Tasks;
using API.Constants;
using API.Entities;
using Microsoft.AspNetCore.Identity;

namespace API.Data
{
    public static class Seed
    {
        public static async Task SeedRoles(RoleManager<AppRole> roleManager)
        {
            var roles = new List<AppRole>
            {
                new() {Name = PolicyConstants.AdminRole},
                new() {Name = PolicyConstants.PlebRole}
            };

            foreach (var role in roles)
            {
                var exists = await roleManager.RoleExistsAsync(role.Name);
                if (!exists)
                {
                    await roleManager.CreateAsync(role);      
                }
            }
        }
    }
}