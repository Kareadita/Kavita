
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

        // Methods
        /// <summary>
        /// If the File on disk's last modified time is after what is stored in MangaFile
        /// </summary>
        /// <returns></returns>
        public bool HasFileBeenModified()
        {
            return File.GetLastWriteTime(FilePath) > LastModified;
        }

        /// <summary>
        /// If the File on disk's last modified time is after passed time
        /// </summary>
        /// <remarks>This is useful to check if the file was modified </remarks>
        /// <param name="time"></param>
        /// <returns></returns>
        public bool HasFileBeenModifiedSince(DateTime? time)
        {
            if (time == null) return true;
            return File.GetLastWriteTime(FilePath) > time;
        }

        /// <summary>
        /// Updates the Last Modified time of the underlying file
        /// </summary>
        public void UpdateLastModified()
        {
            LastModified = File.GetLastWriteTime(FilePath);
        }
    }
}
