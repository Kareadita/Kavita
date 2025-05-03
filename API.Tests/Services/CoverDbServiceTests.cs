using System.Threading.Tasks;
using API.Services;
using API.Services.Tasks.Metadata;
using API.SignalR;
using EasyCaching.Core;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace API.Tests.Services;

public class CoverDbServiceTests : AbstractDbTest
{
    private readonly IDirectoryService _directoryService;
    private readonly IEasyCachingProviderFactory _cacheFactory = Substitute.For<IEasyCachingProviderFactory>();
    private readonly ICoverDbService _coverDbService;
    public CoverDbServiceTests()
    {
        _directoryService = new DirectoryService(Substitute.For<ILogger<DirectoryService>>(), CreateFileSystem());
        var imageService = new ImageService(Substitute.For<ILogger<ImageService>>(), _directoryService);

        _coverDbService = new CoverDbService(Substitute.For<ILogger<CoverDbService>>(), _directoryService, _cacheFactory,
            Substitute.For<IHostEnvironment>(), imageService, UnitOfWork, Substitute.For<IEventHub>());
    }

    protected override Task ResetDb()
    {
        throw new System.NotImplementedException();
    }
}
