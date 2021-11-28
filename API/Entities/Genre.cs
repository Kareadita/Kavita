using System.Collections.Generic;
using API.Entities.Metadata;
using Microsoft.EntityFrameworkCore;

namespace API.Entities
{
    [Index(nameof(NormalizedName), IsUnique = true)]
    public class Genre
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string NormalizedName { get; set; }
        // MetadataUpdate add ProviderId

        public ICollection<SeriesMetadata> SeriesMetadatas { get; set; }
    }
}
