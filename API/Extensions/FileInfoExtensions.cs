using System;
using System.IO;

namespace API.Extensions
{
    public static class FileInfoExtensions
    {
        public static bool DoesLastWriteMatch(this FileInfo fileInfo, DateTime comparison)
        {
            return comparison.Equals(fileInfo.LastWriteTime);
        }
        
        public static bool IsLastWriteLessThan(this FileInfo fileInfo, DateTime comparison)
        {
            return fileInfo.LastWriteTime < comparison;
        }
    }
}