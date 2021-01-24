using API.Interfaces;
using API.Services;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace API.Tests.Services
{
    public class ScannerServiceTests
    {
        private readonly ScannerService _scannerService;
        private readonly ILogger<ScannerService> _logger = Substitute.For<ILogger<ScannerService>>();
        private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
        public ScannerServiceTests()
        {
            _scannerService = new ScannerService(_unitOfWork, _logger);
        }

        // TODO: Start adding tests for how scanner works so we can ensure fallbacks, etc work
    }
}