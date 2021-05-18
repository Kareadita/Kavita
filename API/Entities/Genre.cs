using System.ComponentModel.DataAnnotations;
using API.Entities.Interfaces;

namespace API.Entities
{
    public class Genre : IHasConcurrencyToken
    {
        public int Id { get; set; }
        public string Name { get; set; }
        // TODO: MetadataUpdate add ProviderId

        [ConcurrencyCheck]
        public uint RowVersion { get; set; }

        public void OnSavingChanges()
        {
            RowVersion++;
        }
    }
}