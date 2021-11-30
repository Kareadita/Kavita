using System.Collections.Generic;
using API.Entities.Metadata;
using Microsoft.EntityFrameworkCore;

namespace API.Entities
{
    [Index(nameof(NormalizedName), nameof(ExternalTag), IsUnique = true)]
    public class Genre
    {
        public int Id { get; set; }
        public string Name { get; set; } // TODO: Rename this to Title
        public string NormalizedName { get; set; }
        public bool ExternalTag { get; set; }

        public ICollection<SeriesMetadata> SeriesMetadatas { get; set; }
    }
}
