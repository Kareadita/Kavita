using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;


namespace API.Entities
{
    public class AppUser : IdentityUser<int>
    {
        public DateTime Created { get; set; } = DateTime.Now;
        public DateTime LastActive { get; set; }
        public bool IsAdmin { get; set; }
        public ICollection<Library> Libraries { get; set; }

        [ConcurrencyCheck]
        public uint RowVersion { get; set; }
        
        public ICollection<AppUserRole> UserRoles { get; set; }

        public void OnSavingChanges()
        {
            RowVersion++;
        }

    }
}