using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions.TestingHelpers;
using API.Helpers;
using API.Services;

namespace API.Tests.Services;

public class MetadataServiceTests
{
    private readonly string _testDirectory = Path.Join(Directory.GetCurrentDirectory(), "../../../Services/Test Data/ArchiveService/Archives");
    private const string TestCoverImageFile = "thumbnail.jpg";
    private const string TestCoverArchive = @"c:\file in folder.zip";
    private readonly string _testCoverImageDirectory = Path.Join(Directory.GetCurrentDirectory(), @"../../../Services/Test Data/ArchiveService/CoverImages");
    //private readonly MetadataService _metadataService;
    // private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    // private readonly IImageService _imageService = Substitute.For<IImageService>();
    // private readonly IBookService _bookService = Substitute.For<IBookService>();
    // private readonly IArchiveService _archiveService = Substitute.For<IArchiveService>();
    // private readonly ILogger<MetadataService> _logger = Substitute.For<ILogger<MetadataService>>();
    // private readonly IHubContext<MessageHub> _messageHub = Substitute.For<IHubContext<MessageHub>>();
    private readonly ICacheHelper _cacheHelper;


    public MetadataServiceTests()
    {
        //_metadataService = new MetadataService(_unitOfWork, _logger, _archiveService, _bookService, _imageService, _messageHub);
        var file = new MockFileData("")
        {
            LastWriteTime = DateTimeOffset.Now.Subtract(TimeSpan.FromMinutes(1))
        };
        var fileSystem = new MockFileSystem(new Dictionary<string, MockFileData>
        {
            { TestCoverArchive, file }
        });

        var fileService = new FileService(fileSystem);
        _cacheHelper = new CacheHelper(fileService);
    }
}
