using System.ComponentModel.DataAnnotations;
using API.Entities.Enums;
using API.Entities.Interfaces;

namespace API.Entities
{
    public class Person : IHasConcurrencyToken
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string NormalizedName { get; set; }
        public PersonRole Role { get; set; }

        /// <inheritdoc />
        [ConcurrencyCheck]
        public uint RowVersion { get; private set; }

        /// <inheritdoc />
        public void OnSavingChanges()
        {
            RowVersion++;
        }
    }
}
