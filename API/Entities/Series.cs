using System;
using System.Collections.Generic;
using API.Entities.Enums;
using API.Entities.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace API.Entities
{
    [Index(nameof(Name), nameof(NormalizedName), nameof(LocalizedName), nameof(LibraryId), nameof(Format), IsUnique = true)]
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
        /// Name in original language (Japanese for Manga). By default, will be same as Name.
        /// </summary>
        public string LocalizedName { get; set; }
        /// <summary>
        /// Original Name on disk. Not exposed to UI.
        /// </summary>
        public string OriginalName { get; set; }
        /// <summary>
        /// Summary information related to the Series
        /// </summary>
        public string Summary { get; set; } // NOTE: Migrate into SeriesMetdata (with Metadata update)
        public DateTime Created { get; set; }
        public DateTime LastModified { get; set; }
        /// <summary>
        /// Absolute path to the (managed) image file
        /// </summary>
        /// <remarks>The file is managed internally to Kavita's APPDIR</remarks>
        public string CoverImage { get; set; }
        /// <summary>
        /// Denotes if the CoverImage has been overridden by the user. If so, it will not be updated during normal scan operations.
        /// </summary>
        public bool CoverImageLocked { get; set; }
        /// <summary>
        /// Sum of all Volume page counts
        /// </summary>
        public int Pages { get; set; }

        /// <summary>
        /// The type of all the files attached to this series
        /// </summary>
        public MangaFormat Format { get; set; } = MangaFormat.Unknown;

        public SeriesMetadata Metadata { get; set; }

        // Relationships
        public List<Volume> Volumes { get; set; }
        public Library Library { get; set; }
        public int LibraryId { get; set; }

    }
}
