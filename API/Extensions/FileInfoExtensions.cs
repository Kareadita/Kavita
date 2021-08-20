using System;
using System.IO;

namespace API.Extensions
{
    public static class FileInfoExtensions
    {
        [Obsolete("Please use HasFileBeenModifiedSince")]
        public static bool IsLastWriteLessThan(this FileInfo fileInfo, DateTime comparison)
        {
            return fileInfo?.LastWriteTime < comparison;
        }

        /// <summary>
        /// Checks if the last write time of the file is after the passed date
        /// </summary>
        /// <param name="fileInfo"></param>
        /// <param name="comparison"></param>
        /// <returns></returns>
        public static bool HasFileBeenModifiedSince(this FileInfo fileInfo, DateTime comparison)
        {
            return fileInfo?.LastWriteTime > comparison;
        }
    }
}
