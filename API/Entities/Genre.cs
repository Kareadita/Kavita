using System.ComponentModel.DataAnnotations;
using API.Entities.Interfaces;

namespace API.Entities
{
    public class Genre : IHasConcurrencyToken
    {
        public int Id { get; set; }
        public string Name { get; set; }
        // MetadataUpdate add ProviderId

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
