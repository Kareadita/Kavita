using System.ComponentModel.DataAnnotations;
using API.Entities.Enums;
using API.Entities.Interfaces;

namespace API.Entities
{
    public class Person : IHasConcurrencyToken
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public PersonRole Role { get; set; }

        [ConcurrencyCheck]
        public uint RowVersion { get; set; }

        public void OnSavingChanges()
        {
            RowVersion++;
        }
    }
}