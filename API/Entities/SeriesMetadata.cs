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
        /// <summary>
        /// Gets a collection containing the genres for this item.
        /// </summary>
        public virtual ICollection<Genre> Genres { get; set; }
        public virtual ICollection<Tag> Tags { get; set; }
        public virtual ICollection<Person> Persons { get; set; }
        /// <summary>
        /// Publisher of book or manga/comic
        /// </summary>
        public string Publisher { get; set; }
        
        // Relationship
        public Series Series { get; set; }
        public int SeriesId { get; set; }
        
        [ConcurrencyCheck]
        public uint RowVersion { get; set; }

        public void OnSavingChanges()
        {
            RowVersion++;
        }
    }
}