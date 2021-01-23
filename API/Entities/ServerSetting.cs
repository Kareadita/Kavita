using System.ComponentModel.DataAnnotations;
using API.Entities.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace API.Entities
{
    public class ServerSetting : IHasConcurrencyToken
    {
        [Key]
        public string Key { get; set; }
        public string Value { get; set; }

        [ConcurrencyCheck]
        public uint RowVersion { get; set; }
        public void OnSavingChanges()
        {
            RowVersion++;
        }
    }
}