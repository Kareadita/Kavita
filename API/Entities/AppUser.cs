using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using API.Entities.Interfaces;
using Microsoft.AspNetCore.Identity;


namespace API.Entities
{
    public class AppUser : IdentityUser<int>, IHasConcurrencyToken
    {
        public DateTime Created { get; set; } = DateTime.Now;
        public DateTime LastActive { get; set; }
        public ICollection<Library> Libraries { get; set; }
        
        
        public ICollection<AppUserRole> UserRoles { get; set; }
        
        //public ICollection<SeriesProgress> SeriesProgresses { get; set; }
        public ICollection<AppUserProgress> Progresses { get; set; }
        
        [ConcurrencyCheck]
        public uint RowVersion { get; set; }

        public void OnSavingChanges()
        {
            RowVersion++;
        }

    }
}