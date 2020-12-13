using System;
using System.ComponentModel.DataAnnotations;
using API.Entities.Interfaces;


namespace API.Entities
{
    public class AppUser : IHasConcurrencyToken
    {
        public int Id { get; set; }
        public string UserName { get; set; }
        public byte[] PasswordHash { get; set; }
        public byte[] PasswordSalt { get; set; }
        public DateTime Created { get; set; } = DateTime.Now;
        public DateTime LastActive { get; set; }
        public bool IsAdmin { get; set; }

        [ConcurrencyCheck]
        public uint RowVersion { get; set; }

        public void OnSavingChanges()
        {
            RowVersion++;
        }

    }
}