using System.Threading.Tasks;
using EasyCaching.Core;

namespace API.Services.Plus;

public interface ILicenseService
{
    Task<bool> IsLicenseValid(string license);
}

public class LicenseService : ILicenseService
{
    private readonly IEasyCachingProviderFactory _cachingProviderFactory;

    public LicenseService(IEasyCachingProviderFactory cachingProviderFactory)
    {
        _cachingProviderFactory = cachingProviderFactory;
    }

    public Task<bool> IsLicenseValid(string license)
    {
        if (string.IsNullOrEmpty(license)) return Task.FromResult(false);
        return Task.FromResult(true);
    }
}
