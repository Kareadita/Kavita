using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using API.Entities.Enums;
using API.Entities.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace API.Entities.Metadata
{
    [Index(nameof(Id), nameof(SeriesId), IsUnique = true)]
    public class SeriesMetadata : IHasConcurrencyToken
    {
        public int Id { get; set; }

        public string Summary { get; set; }

        public ICollection<CollectionTag> CollectionTags { get; set; }

        public ICollection<Genre> Genres { get; set; } = new List<Genre>();
        public ICollection<Tag> Tags { get; set; } = new List<Tag>();
        /// <summary>
        /// All people attached at a Series level.
        /// </summary>
        public ICollection<Person> People { get; set; } = new List<Person>();

        /// <summary>
        /// Highest Age Rating from all Chapters
        /// </summary>
        public AgeRating AgeRating { get; set; }
        /// <summary>
        /// Earliest Year from all chapters
        /// </summary>
        public int ReleaseYear { get; set; }
        /// <summary>
        /// Language of the content (ISO 639-1 code)
        /// </summary>
        public string Language { get; set; } = string.Empty;
        /// <summary>
        /// Total number of issues in the series
        /// </summary>
        public int Count { get; set; } = 0;
        public PublicationStatus PublicationStatus { get; set; }

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
