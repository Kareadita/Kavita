using API.Services;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace API.Tests.Services
{

    public class DirectoryServiceTests
    {
        private readonly DirectoryService _directoryService;
        private readonly ILogger<DirectoryService> _logger = Substitute.For<ILogger<DirectoryService>>();

        public DirectoryServiceTests()
        {
            _directoryService = new DirectoryService(_logger);
        }

        [Fact]
        public void GetFiles_Test()
        {
            //_directoryService.GetFiles()
        }

        [Fact]
        public void ListDirectory_Test()
        {
            
        }
    }
}