using System.Threading;
using System.Threading.Tasks;
using Kavita.Models.Entities.Enums;
using Kavita.Models.Entities.Interfaces;
using Kavita.Models.Entities.Person;
using Kavita.Models.Images;

namespace Kavita.API.Services.Metadata;

public interface ICoverDbService
{
    Task<string> DownloadFaviconAsync(string url, EncodeFormat encodeFormat, CancellationToken ct = default);
    Task<string> DownloadPublisherImageAsync(string publisherName, EncodeFormat encodeFormat, CancellationToken ct = default);
    /// <summary>
    /// Returns the remote CoversDB url for a Person, without downloading or applying it
    /// </summary>
    Task<string?> GetPersonImageUrlAsync(Person person, CancellationToken ct = default);
    /// <summary>
    /// Downloads the url to temp and writes it through <see cref="IImageFileService.SetCoverAsync"/>, which commits
    /// and sends the CoverUpdate
    /// </summary>
    Task SetCoverByUrlAsync(IHasCoverImageFile entity, string url, CoverWrite write, CancellationToken ct = default);
    /// <summary>
    /// Takes an id so it can run as a Hangfire job
    /// </summary>
    Task SetOidcUserCoverAsync(int userId, string url, CancellationToken ct = default);
}
