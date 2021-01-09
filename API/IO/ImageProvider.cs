using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using API.Extensions;
using NetVips;

namespace API.IO
{
    public static class ImageProvider
    {
        /// <summary>
        /// Generates byte array of cover image.
        /// Given a path to a compressed file (zip, rar, cbz, cbr, etc), will ensure the first image is returned unless
        /// a folder.extension exists in the root directory of the compressed file.
        /// </summary>
        /// <param name="filepath"></param>
        /// <param name="createThumbnail">Create a smaller variant of file extracted from archive. Archive images are usually 1MB each.</param>
        /// <returns></returns>
        public static byte[] GetCoverImage(string filepath, bool createThumbnail = false)
        {
            if (!File.Exists(filepath) || !Parser.Parser.IsArchive(filepath)) return Array.Empty<byte>();

            using ZipArchive archive = ZipFile.OpenRead(filepath);
            if (!archive.HasFiles()) return Array.Empty<byte>();
            
            

            var folder = archive.Entries.SingleOrDefault(x => Path.GetFileNameWithoutExtension(x.Name).ToLower() == "folder");
            var entry = archive.Entries.Where(x => Path.HasExtension(x.FullName)).OrderBy(x => x.FullName).ToList()[0];

            if (folder != null)
            {
                entry = folder;
            }

            if (createThumbnail)
            {
                try
                {
                    using var stream = entry.Open();
                    var thumbnail = Image.ThumbnailStream(stream, 320); 
                    Console.WriteLine(thumbnail.ToString());
                    return thumbnail.WriteToBuffer(".jpg");
                }
                catch (Exception ex)
                {
                    Console.WriteLine("There was a critical error and prevented thumbnail generation.");
                    Console.WriteLine(ex.Message);
                }
            }
            
            return ExtractEntryToImage(entry);
        }
        
        private static byte[] ExtractEntryToImage(ZipArchiveEntry entry)
        {
            using var stream = entry.Open();
            using var ms = new MemoryStream();
            stream.CopyTo(ms);
            var data = ms.ToArray();

            return data;
        }
    }
}