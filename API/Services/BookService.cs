using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using API.Archive;
using API.Controllers;
using API.Entities.Enums;
using API.Entities.Interfaces;
using API.Extensions;
using API.Interfaces.Services;
using API.Parser;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.ObjectPool;
using NetVips;
using SharpCompress.Archives;
using SharpCompress.Common;
using VersOne.Epub;

namespace API.Services
{
    public class BookService : IBookService
    {
        private readonly ILogger<BookService> _logger;
        private readonly IArchiveService _archiveService;
        private readonly IDirectoryService _directoryService;

        private const int ThumbnailWidth = 320; // 153w x 230h
        //private readonly ObjectPool<EpubBook> _readerPool;
        

        public static readonly Regex StyleSheetKeyRegex = new Regex("href=\"(?<Key>[a-z0-9\\./#-]*)\"", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public BookService(ILogger<BookService> logger, IArchiveService archiveService, IDirectoryService directoryService)
        {
            _logger = logger;
            _archiveService = archiveService;
            _directoryService = directoryService;
            //_readerPool = new DefaultObjectPool<EpubBook>();
        }

        public static string GetHrefKey(string htmlContent)
        {
            var matches = StyleSheetKeyRegex.Matches(htmlContent);
            foreach (Match match in matches)
            {
                if (match.Groups["Key"].Success && match.Groups["Key"].Value != string.Empty)
                {
                    return match.Groups["Key"].Value;
                }
            }

            return string.Empty;
        }
        
        public static ICollection<string> GetHrefKeys(string htmlContent)
        {
            var keys = new List<string>();
            var matches = StyleSheetKeyRegex.Matches(htmlContent);
            foreach (Match match in matches)
            {
                if (match.Groups["Key"].Success && match.Groups["Key"].Value != string.Empty)
                {
                    keys.Add(match.Groups["Key"].Value);
                }
            }

            return keys;
        }

        private bool IsValidFile(string filePath)
        {
            if (!File.Exists(filePath))
            {
                _logger.LogError("Book {ArchivePath} could not be found", filePath);
                return false;
            }

            if (Parser.Parser.IsBook(filePath)) return true;
            
            _logger.LogError("Book {ArchivePath} is not a valid EPUB", filePath);
            return false; 
        }

        public int GetNumberOfPages(string filePath)
        {
            if (!IsValidFile(filePath) || !Parser.Parser.IsEpub(filePath)) return 0;

            var epubBook = EpubReader.ReadBook(filePath);
            return epubBook.Content.Html.Count;
        }

        public static string CleanContentKeys(string key)
        {
            return key.Replace("../", string.Empty);
        }

        public async Task<Dictionary<string, int>> CreateKeyToPageMappingAsync(EpubBookRef book)
        {
            var dict = new Dictionary<string, int>();
            int pageCount = 0;
            foreach (var contentFileRef in await book.GetReadingOrderAsync())
            {
                if (contentFileRef.ContentType == EpubContentType.XHTML_1_1)
                {
                    dict.Add(contentFileRef.FileName, pageCount);
                    pageCount += 1;    
                }
            }
            
            return dict;
        }

        public ParserInfo ParseInfo(string filePath)
        {
            // TODO: Use a pool of EpubReaders since we are going to create these a lot
            var epubBook = EpubReader.ReadBook(filePath);
            
            return new ParserInfo()
            {
                Chapters = "0",
                Edition = "",
                Format = MangaFormat.Book,
                FullFilePath = filePath,
                IsSpecial = false,
                Series = epubBook.Title,
                Volumes = "0"
            };
        }

        public byte[] GetCoverImage(string fileFilePath, bool createThumbnail = true)
        {
            if (!IsValidFile(fileFilePath)) return Array.Empty<byte>();
            
            var epubBook = EpubReader.ReadBook(fileFilePath);
            
            
            try
            {
                var coverImageContent = epubBook.CoverImage ?? epubBook.Content.Images.Values.First().Content;

                if (coverImageContent != null && createThumbnail)
                {
                    using var stream = new MemoryStream(coverImageContent);

                    using var thumbnail = Image.ThumbnailStream(stream, ThumbnailWidth);
                    return thumbnail.WriteToBuffer(".jpg");
                }

                return coverImageContent;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "There was a critical error and prevented thumbnail generation on {BookFile}. Defaulting to no cover image", fileFilePath);
            }
            
            

            return Array.Empty<byte>();
            
        }

        public void ExtractToFolder(string archivePath, string extractPath)
        {
            if (!_archiveService.IsValidArchive(archivePath)) return;

            if (Directory.Exists(extractPath)) return;
            
            var sw = Stopwatch.StartNew();
            

            try
            {
                var libraryHandler = _archiveService.CanOpen(archivePath);
                switch (libraryHandler)
                {
                    case ArchiveLibrary.Default:
                    {
                        _logger.LogDebug("Using default compression handling");
                        using var archive = ZipFile.OpenRead(archivePath);
                        ExtractArchiveEntries(archive, extractPath);
                        break;
                    }
                    case ArchiveLibrary.SharpCompress:
                    {
                        _logger.LogDebug("Using SharpCompress compression handling");
                        using var archive = ArchiveFactory.Open(archivePath);
                        ExtractArchiveEntities(archive.Entries, extractPath);
                        break;
                    }
                    case ArchiveLibrary.NotSupported:
                        _logger.LogError("[ExtractArchive] This archive cannot be read: {ArchivePath}. Defaulting to 0 pages", archivePath);
                        return;
                    default:
                        _logger.LogError("[ExtractArchive] There was an exception when reading archive stream: {ArchivePath}. Defaulting to 0 pages", archivePath);
                        return;
                }
                
            }
            catch (Exception e)
            {
                _logger.LogError(e, "There was a problem extracting {ArchivePath} to {ExtractPath}",archivePath, extractPath);
                return;
            }
            _logger.LogDebug("Extracted archive to {ExtractPath} in {ElapsedMilliseconds} milliseconds", extractPath, sw.ElapsedMilliseconds);
        }

        public static string RemoveWhiteSpaceFromStylesheets(string body)
        {
            body = Regex.Replace(body, @"[a-zA-Z]+#", "#");
            body = Regex.Replace(body, @"[\n\r]+\s*", string.Empty);
            body = Regex.Replace(body, @"\s+", " ");
            body = Regex.Replace(body, @"\s?([:,;{}])\s?", "$1");
            body = body.Replace(";}", "}");
            body = Regex.Replace(body, @"([\s:]0)(px|pt|%|em)", "$1");

            // Remove comments from CSS
            body = Regex.Replace(body, @"/\*[\d\D]*?\*/", string.Empty);

            return body;
        }

        private static void ExtractArchiveEntities(IEnumerable<IArchiveEntry> entries, string extractPath)
        {
            DirectoryService.ExistOrCreate(extractPath);
            foreach (var entry in entries)
            {
                entry.WriteToDirectory(extractPath, new ExtractionOptions()
                {
                    ExtractFullPath = false,
                    Overwrite = false
                });
            }
        }

        private void ExtractArchiveEntries(ZipArchive archive, string extractPath)
        {
            if (!archive.HasFiles()) return;
            archive.ExtractToDirectory(extractPath, true);
        }
    }
}