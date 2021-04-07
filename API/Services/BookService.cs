using System;
using System.IO;
using System.Linq;
using API.Entities.Enums;
using API.Entities.Interfaces;
using API.Parser;
using Microsoft.Extensions.Logging;
using NetVips;
using VersOne.Epub;

namespace API.Services
{
    public class BookService : IBookService
    {
        private readonly ILogger<BookService> _logger;
        private const int ThumbnailWidth = 320; // 153w x 230h

        public BookService(ILogger<BookService> logger)
        {
            _logger = logger;
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

    }
}