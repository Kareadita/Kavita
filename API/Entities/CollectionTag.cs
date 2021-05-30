using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using API.Entities.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace API.Entities
{
    /// <summary>
    /// Represents a user entered field that is used as a tagging and grouping mechanism
    /// </summary>
    [Index(nameof(Id), nameof(Promoted), IsUnique = true)]
    public class CollectionTag : IHasConcurrencyToken
    {
        public int Id { get; set; }
        /// <summary>
        /// Visible title of the Tag
        /// </summary>
        public string Title { get; set; }
        
        /// <summary>
        /// Cover Image for the collection tag
        /// </summary>
        public byte[] CoverImage { get; set; }
        
        /// <summary>
        /// A description of the tag
        /// </summary>
        public string Summary { get; set; }

        /// <summary>
        /// A normalized string used to check if the tag already exists in the DB
        /// </summary>
        public string NormalizedTitle { get; set; }
        /// <summary>
        /// A promoted collection tag will allow all linked seriesMetadata's Series to show for all users.
        /// </summary>
        public bool Promoted { get; set; }
        
        public ICollection<SeriesMetadata> SeriesMetadatas { get; set; }
        

        [ConcurrencyCheck]
        public uint RowVersion { get; set; }

        public void OnSavingChanges()
        {
            RowVersion++;
        }
    }
}