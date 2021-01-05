using System;
using System.IO;
using System.IO.Compression;
using System.Linq;

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
        /// <returns></returns>
        public static byte[] GetCoverImage(string filepath)
        {
            if (!File.Exists(filepath) || !Parser.Parser.IsArchive(filepath)) return Array.Empty<byte>();

            using ZipArchive archive = ZipFile.OpenRead(filepath);
            if (archive.Entries.Count <= 0) return Array.Empty<byte>();

            var folder = archive.Entries.SingleOrDefault(x => Path.GetFileNameWithoutExtension(x.Name).ToLower() == "folder");
            var entry = archive.Entries[0];
              
            if (folder != null)
            {
                entry = folder;
            }
              
            return ExtractEntryToImage(entry);
        }

        private static byte[] ExtractEntryToImage(ZipArchiveEntry entry)
        {
            var stream = entry.Open();
            using var ms = new MemoryStream();
            stream.CopyTo(ms);
            var data = ms.ToArray();

            return data;
        }
    }
}