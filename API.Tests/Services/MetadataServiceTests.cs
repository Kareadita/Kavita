using System;
using API.Entities;
using API.Interfaces;
using API.Interfaces.Services;
using API.Services;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace API.Tests.Services
{
    public class MetadataServiceTests
    {
        // private readonly MetadataService _metadataService;
        // private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
        // private readonly IImageService _imageService = Substitute.For<IImageService>();
        // private readonly IBookService _bookService = Substitute.For<IBookService>();
        // private readonly IArchiveService _archiveService = Substitute.For<IArchiveService>();
        // private readonly ILogger<MetadataService> _logger = Substitute.For<ILogger<MetadataService>>();
        //
        // public MetadataServiceTests()
        // {
        //     _metadataService = new MetadataService(_unitOfWork, _logger, _archiveService, _bookService, _imageService);
        // }

        [Fact]
        public void ShouldUpdateCoverImage_ShouldReturnFalse()
        {
            Assert.False(MetadataService.ShouldUpdateCoverImage(null, new MangaFile()
            {
                FilePath = String.Empty,
                LastModified = DateTime.Now
            }, false, false));
        }
    }
}
