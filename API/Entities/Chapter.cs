using System;
using System.Collections.Generic;
using API.Entities.Enums;
using API.Entities.Interfaces;
using API.Entities.Metadata;
using API.Parser;

namespace API.Entities
{
    public class Chapter : IEntityDate
    {
        public int Id { get; set; }
        /// <summary>
        /// Range of numbers. Chapter 2-4 -> "2-4". Chapter 2 -> "2".
        /// </summary>
        public string Range { get; set; }
        /// <summary>
        /// Smallest number of the Range. Can be a partial like Chapter 4.5
        /// </summary>
        public string Number { get; set; }
        /// <summary>
        /// The files that represent this Chapter
        /// </summary>
        public ICollection<MangaFile> Files { get; set; }
        public DateTime Created { get; set; }
        public DateTime LastModified { get; set; }
        /// <summary>
        /// Absolute path to the (managed) image file
        /// </summary>
        /// <remarks>The file is managed internally to Kavita's APPDIR</remarks>
        public string CoverImage { get; set; }
        public bool CoverImageLocked { get; set; }
        /// <summary>
        /// Total number of pages in all MangaFiles
        /// </summary>
        public int Pages { get; set; }
        /// <summary>
        /// If this Chapter contains files that could only be identified as Series or has Special Identifier from filename
        /// </summary>
        public bool IsSpecial { get; set; }
        /// <summary>
        /// Used for books/specials to display custom title. For non-specials/books, will be set to <see cref="Range"/>
        /// </summary>
        public string Title { get; set; }


        /// <summary>
        /// Chapter title
        /// </summary>
        /// <remarks>This should not be confused with Title which is used for special filenames.</remarks>
        public string TitleName { get; set; } = string.Empty;
        // public string Year { get; set; } // Only time I can think this will be more than 1 year is for a volume which will be a spread


        /// <summary>
        /// All people attached at a Chapter level. Usually Comics will have different people per issue.
        /// </summary>
        public ICollection<Person> People { get; set; } = new List<Person>();




        // Relationships
        public Volume Volume { get; set; }
        public int VolumeId { get; set; }

        //public ChapterMetadata ChapterMetadata { get; set; }
        //public int ChapterMetadataId { get; set; }

        public void UpdateFrom(ParserInfo info)
        {
            Files ??= new List<MangaFile>();
            IsSpecial = info.IsSpecialInfo();
            if (IsSpecial)
            {
                Number = "0";
            }
            Title = (IsSpecial && info.Format == MangaFormat.Epub)
                ? info.Title
                : Range;

        }
    }
}
