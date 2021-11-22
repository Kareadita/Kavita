using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using API.Entities.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace API.Entities
{
    [Index(nameof(Id), nameof(SeriesId), IsUnique = true)]
    public class SeriesMetadata : IHasConcurrencyToken
    {
        public int Id { get; set; }

        public string Summary { get; set; }
        // TODO: AgeRating, CommunityRating,


        public ICollection<CollectionTag> CollectionTags { get; set; }

        public ICollection<Genre> Genres { get; set; } = new List<Genre>();
        /// <summary>
        /// All people attached at a Series level.
        /// </summary>
        public ICollection<Person> People { get; set; } = new List<Person>();


        // Relationship
        public Series Series { get; set; }
        public int SeriesId { get; set; }

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
