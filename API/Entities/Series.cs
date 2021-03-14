using System;
using System.Collections.Generic;
using API.Entities.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace API.Entities
{
    [Index(nameof(Name), nameof(NormalizedName), nameof(LocalizedName), IsUnique = true)]
    public class Series : IEntityDate
    {
        public int Id { get; set; }
        /// <summary>
        /// The UI visible Name of the Series. This may or may not be the same as the OriginalName
        /// </summary>
        public string Name { get; set; }
        /// <summary>
        /// Used internally for name matching. <see cref="Parser.Parser.Normalize"/>
        /// </summary>
        public string NormalizedName { get; set; }
        /// <summary>
        /// The name used to sort the Series. By default, will be the same as Name.
        /// </summary>
        public string SortName { get; set; }
        /// <summary>
        /// Name in Japanese. By default, will be same as Name. 
        /// </summary>
        public string LocalizedName { get; set; }
        /// <summary>
        /// Original Name on disk. Not exposed to UI.
        /// </summary>
        public string OriginalName { get; set; }
        /// <summary>
        /// Summary information related to the Series
        /// </summary>
        public string Summary { get; set; }
        public DateTime Created { get; set; }
        public DateTime LastModified { get; set; }
        public byte[] CoverImage { get; set; }
        // NOTE: Do I want to store a thumbImage for search results? 
        /// <summary>
        /// Sum of all Volume page counts
        /// </summary>
        public int Pages { get; set; }

        // Relationships
        public List<Volume> Volumes { get; set; }
        public Library Library { get; set; }
        public int LibraryId { get; set; }
    }
}