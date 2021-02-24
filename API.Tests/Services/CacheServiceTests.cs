using Xunit;

namespace API.Tests.Services
{
    public class CacheServiceTests
    {
        // private readonly CacheService _cacheService;
        // private readonly ILogger<CacheService> _logger = Substitute.For<ILogger<CacheService>>();
        // private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
        // private readonly IArchiveService _archiveService = Substitute.For<IArchiveService>();
        // private readonly IDirectoryService _directoryService = Substitute.For<DirectoryService>();

        // public CacheServiceTests()
        // {
        //     //_cacheService = new CacheService(_logger, _unitOfWork, _archiveService, _directoryService);
        // }
        
        //string GetCachedPagePath(Volume volume, int page)
        [Fact]
        //[InlineData("", 0, "")]
        public void GetCachedPagePathTest_Should()
        {
            // TODO: Figure out how to test this 
            // string archivePath = "flat file.zip";
            // int pageNum = 0;
            // string expected = "cache/1/pexels-photo-6551949.jpg";
            //
            // var testDirectory = Path.Join(Directory.GetCurrentDirectory(), "../../../Services/Test Data/ArchiveService/Archives");
            // var file = Path.Join(testDirectory, archivePath);
            // var volume = new Volume
            // {
            //     Id = 1,
            //     Files = new List<MangaFile>()
            //     {
            //         new()
            //         {
            //             Id = 1,
            //             Chapter = 0,
            //             FilePath = archivePath,
            //             Format = MangaFormat.Archive,
            //             NumberOfPages = 1,
            //         }
            //     },
            //     Name = "1",
            //     Number = 1
            // };
            //
            // var cacheService = Substitute.ForPartsOf<CacheService>();
            // cacheService.Configure().CacheDirectoryIsAccessible().Returns(true);
            // cacheService.Configure().GetVolumeCachePath(1, volume.Files.ElementAt(0)).Returns("cache/1/");
            // _directoryService.Configure().GetFilesWithExtension("cache/1/").Returns(new string[] {"pexels-photo-6551949.jpg"});
            // Assert.Equal(expected, _cacheService.GetCachedPagePath(volume, pageNum));
            Assert.True(true);
        }

        [Fact]
        public void GetOrderedChaptersTest()
        {
            // var files = new List<Chapter>()
            // {
            //     new()
            //     {
            //         Number = "1"
            //     },
            //     new()
            //     {
            //         Chapter = 2
            //     },
            //     new()
            //     {
            //         Chapter = 0
            //     },
            // };
            // var expected = new List<MangaFile>()
            // {
            //     new()
            //     {
            //         Chapter = 1
            //     },
            //     new()
            //     {
            //         Chapter = 2
            //     },
            //     new()
            //     {
            //         Chapter = 0
            //     },
            // };
            // Assert.NotStrictEqual(expected, _cacheService.GetOrderedChapters(files));
        }
        
        
    }
}