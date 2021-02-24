using API.Interfaces;
using API.Services;
using API.Services.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace API.Tests.Services
{
    public class BackupServiceTests
    {
        private readonly DirectoryService _directoryService;
        private readonly BackupService _backupService;
        private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
        private readonly ILogger<DirectoryService> _directoryLogger = Substitute.For<ILogger<DirectoryService>>();
        private readonly ILogger<BackupService> _logger = Substitute.For<ILogger<BackupService>>();
        private readonly IConfiguration _config;

        // public BackupServiceTests()
        // {
        //     var inMemorySettings = new Dictionary<string, string> {
        //         {"Logging:File:MaxRollingFiles", "0"},
        //         {"Logging:File:Path", "file.log"},
        //     };
        //     
        //     _config = new ConfigurationBuilder()
        //         .AddInMemoryCollection(inMemorySettings)
        //         .Build();
        //     
        //     //_config.GetMaxRollingFiles().Returns(0);
        //     //_config.GetLoggingFileName().Returns("file.log");
        //     //var testDirectory = Path.Join(Directory.GetCurrentDirectory(), "../../../Services/Test Data/BackupService/");
        //     //Directory.GetCurrentDirectory().Returns(testDirectory);
        //     
        //     _directoryService = new DirectoryService(_directoryLogger);
        //     _backupService = new BackupService(_unitOfWork, _logger, _directoryService, _config);
        // }
        //
        // [Fact]
        // public void Test()
        // {
        //     _backupService.BackupDatabase();
        // }
        
        
    }
}