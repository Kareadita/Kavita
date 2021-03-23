using System;
using System.IO;
using System.IO.Compression;
using SharpCompress.Archives;

namespace API.Archive
{
    public static class Archive
    {
        /// <summary>
        /// Checks if a File can be opened. Requires up to 2 opens of the filestream.
        /// </summary>
        /// <param name="archivePath"></param>
        /// <returns></returns>
        public static ArchiveLibrary CanOpen(string archivePath)
        {
            if (!File.Exists(archivePath) || !Parser.Parser.IsArchive(archivePath)) return ArchiveLibrary.NotSupported;
            
            try
            {
                using var a2 = ZipFile.OpenRead(archivePath);
                return ArchiveLibrary.Default;
                
            }
            catch (Exception)
            {
                try
                {
                    using var a1 = ArchiveFactory.Open(archivePath);
                    return ArchiveLibrary.SharpCompress;
                }
                catch (Exception)
                {
                    return ArchiveLibrary.NotSupported;
                }
            }
        }
        
        
    }
}