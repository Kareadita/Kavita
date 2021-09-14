using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data.Common;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using API.Data;
using API.Entities;
using API.Entities.Enums;
using API.Interfaces;
using API.Interfaces.Services;
using API.Parser;
using API.Services;
using API.Services.Tasks;
using API.Services.Tasks.Scanner;
using API.SignalR;
using API.Tests.Helpers;
using AutoMapper;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace API.Tests.Services
{
    public class ScannerServiceTests : IDisposable
    {
        private readonly ScannerService _scannerService;
        private readonly ILogger<ScannerService> _logger = Substitute.For<ILogger<ScannerService>>();
        private readonly IArchiveService _archiveService = Substitute.For<IArchiveService>();
        private readonly IBookService _bookService = Substitute.For<IBookService>();
        private readonly IImageService _imageService = Substitute.For<IImageService>();
        private readonly ILogger<MetadataService> _metadataLogger = Substitute.For<ILogger<MetadataService>>();
        private readonly ICacheService _cacheService = Substitute.For<ICacheService>();
        private readonly IHubContext<MessageHub> _messageHub = Substitute.For<IHubContext<MessageHub>>();

        private readonly DbConnection _connection;
        private readonly DataContext _context;


        public ScannerServiceTests()
        {
            var contextOptions = new DbContextOptionsBuilder()
                .UseSqlite(CreateInMemoryDatabase())
                .Options;
            _connection = RelationalOptionsExtension.Extract(contextOptions).Connection;

            _context = new DataContext(contextOptions);
            Task.Run(SeedDb).GetAwaiter().GetResult();

            IUnitOfWork unitOfWork = new UnitOfWork(_context, Substitute.For<IMapper>(), null);


            IMetadataService metadataService = Substitute.For<MetadataService>(unitOfWork, _metadataLogger, _archiveService, _bookService, _imageService);
            _scannerService = new ScannerService(unitOfWork, _logger, _archiveService, metadataService, _bookService, _cacheService, _messageHub);
        }

        private async Task<bool> SeedDb()
        {
            await _context.Database.MigrateAsync();
            await Seed.SeedSettings(_context);

            _context.Library.Add(new Library()
            {
                Name = "Manga",
                Folders = new List<FolderPath>()
                {
                    new FolderPath()
                    {
                        Path = Path.Join(Directory.GetCurrentDirectory(), "../../../Services/Test Data/ScannerService/Manga")
                    }
                }
            });
            return await _context.SaveChangesAsync() > 0;
        }

        [Fact]
        public void FindSeriesNotOnDisk_Should_RemoveNothing_Test()
        {
            var infos = new Dictionary<ParsedSeries, List<ParserInfo>>();

            AddToParsedInfo(infos, new ParserInfo() {Series = "Darker than Black", Format = MangaFormat.Archive});
            AddToParsedInfo(infos, new ParserInfo() {Series = "Cage of Eden", Volumes = "1", Format = MangaFormat.Archive});
            AddToParsedInfo(infos, new ParserInfo() {Series = "Cage of Eden", Volumes = "10", Format = MangaFormat.Archive});

            var existingSeries = new List<Series>
            {
                new Series()
                {
                    Name = "Cage of Eden",
                    LocalizedName = "Cage of Eden",
                    OriginalName = "Cage of Eden",
                    NormalizedName = API.Parser.Parser.Normalize("Cage of Eden"),
                    Metadata = new SeriesMetadata(),
                    Format = MangaFormat.Archive
                },
                new Series()
                {
                    Name = "Darker Than Black",
                    LocalizedName = "Darker Than Black",
                    OriginalName = "Darker Than Black",
                    NormalizedName = API.Parser.Parser.Normalize("Darker Than Black"),
                    Metadata = new SeriesMetadata(),
                    Format = MangaFormat.Archive
                }
            };



            Assert.Empty(_scannerService.FindSeriesNotOnDisk(existingSeries, infos));
        }


        // TODO: Figure out how to do this with ParseScannedFiles
        // [Theory]
        // [InlineData(new [] {"Darker than Black"}, "Darker than Black", "Darker than Black")]
        // [InlineData(new [] {"Darker than Black"}, "Darker Than Black", "Darker than Black")]
        // [InlineData(new [] {"Darker than Black"}, "Darker Than Black!", "Darker than Black")]
        // [InlineData(new [] {""}, "Runaway Jack", "Runaway Jack")]
        // public void MergeNameTest(string[] existingSeriesNames, string parsedInfoName, string expected)
        // {
        //     var collectedSeries = new ConcurrentDictionary<ParsedSeries, List<ParserInfo>>();
        //     foreach (var seriesName in existingSeriesNames)
        //     {
        //         AddToParsedInfo(collectedSeries, new ParserInfo() {Series = seriesName, Format = MangaFormat.Archive});
        //     }
        //
        //     var actualName = new ParseScannedFiles(_bookService, _logger).MergeName(collectedSeries, new ParserInfo()
        //     {
        //         Series = parsedInfoName,
        //         Format = MangaFormat.Archive
        //     });
        //
        //     Assert.Equal(expected, actualName);
        // }

        [Fact]
        public void RemoveMissingSeries_Should_RemoveSeries()
        {
            var existingSeries = new List<Series>()
            {
                EntityFactory.CreateSeries("Darker than Black Vol 1"),
                EntityFactory.CreateSeries("Darker than Black"),
                EntityFactory.CreateSeries("Beastars"),
            };
            var missingSeries = new List<Series>()
            {
                EntityFactory.CreateSeries("Darker than Black Vol 1"),
            };
            existingSeries = ScannerService.RemoveMissingSeries(existingSeries, missingSeries, out var removeCount).ToList();

            Assert.DoesNotContain(missingSeries[0].Name, existingSeries.Select(s => s.Name));
            Assert.Equal(missingSeries.Count, removeCount);
        }

        private void AddToParsedInfo(IDictionary<ParsedSeries, List<ParserInfo>> collectedSeries, ParserInfo info)
        {
            var existingKey = collectedSeries.Keys.FirstOrDefault(ps =>
                ps.Format == info.Format && ps.NormalizedName == API.Parser.Parser.Normalize(info.Series));
            existingKey ??= new ParsedSeries()
            {
                Format = info.Format,
                Name = info.Series,
                NormalizedName = API.Parser.Parser.Normalize(info.Series)
            };
            if (collectedSeries.GetType() == typeof(ConcurrentDictionary<,>))
            {
                ((ConcurrentDictionary<ParsedSeries, List<ParserInfo>>) collectedSeries).AddOrUpdate(existingKey, new List<ParserInfo>() {info}, (_, oldValue) =>
                {
                    oldValue ??= new List<ParserInfo>();
                    if (!oldValue.Contains(info))
                    {
                        oldValue.Add(info);
                    }

                    return oldValue;
                });
            }
            else
            {
                if (!collectedSeries.ContainsKey(existingKey))
                {
                    collectedSeries.Add(existingKey, new List<ParserInfo>() {info});
                }
                else
                {
                    var list = collectedSeries[existingKey];
                    if (!list.Contains(info))
                    {
                        list.Add(info);
                    }

                    collectedSeries[existingKey] = list;
                }

            }

        }

        private static DbConnection CreateInMemoryDatabase()
        {
            var connection = new SqliteConnection("Filename=:memory:");

            connection.Open();

            return connection;
        }

        public void Dispose() => _connection.Dispose();
    }
}
