
using System;
using System.IO;
using API.Entities.Enums;

namespace API.Entities
{
    /// <summary>
    /// Represents a wrapper to the underlying file. This provides information around file, like number of pages, format, etc.
    /// </summary>
    public class MangaFile
    {
        public int Id { get; set; }
        /// <summary>
        /// Absolute path to the archive file
        /// </summary>
        public string FilePath { get; set; }
        /// <summary>
        /// Number of pages for the given file
        /// </summary>
        public int Pages { get; set; }
        public MangaFormat Format { get; set; }
        /// <summary>
        /// Last time underlying file was modified
        /// </summary>
        public DateTime LastModified { get; set; }

        // NOTE: Should I add HasComicInfo.xml?

        // Relationship Mapping
        public Chapter Chapter { get; set; }
        public int ChapterId { get; set; }


        /// <summary>
        /// Updates the Last Modified time of the underlying file
        /// </summary>
        public void UpdateLastModified()
        {
            LastModified = File.GetLastWriteTime(FilePath);
        }
    }
}
