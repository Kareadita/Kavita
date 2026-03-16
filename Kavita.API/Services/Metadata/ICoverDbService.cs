using System.Threading;
using System.Threading.Tasks;
using Kavita.Models.Entities;
using Kavita.Models.Entities.Enums;
using Kavita.Models.Entities.Person;
using Kavita.Models.Entities.User;

namespace Kavita.API.Services.Metadata;

public interface ICoverDbService
{
    Task<string> DownloadFaviconAsync(string url, EncodeFormat encodeFormat, CancellationToken ct = default);
    Task<string> DownloadPublisherImageAsync(string publisherName, EncodeFormat encodeFormat, CancellationToken ct = default);
    Task<string?> DownloadPersonImageAsync(Person person, EncodeFormat encodeFormat, CancellationToken ct = default);
    Task<string?> DownloadPersonImageAsync(Person person, EncodeFormat encodeFormat, string url, CancellationToken ct = default);
    Task SetPersonCoverByUrl(Person person, string url, bool fromBase64 = true, bool checkNoImagePlaceholder = false, bool chooseBetterImage = true, CancellationToken ct = default);
    Task SetSeriesCoverByUrl(Series series, string url, bool fromBase64 = true, bool chooseBetterImage = false, CancellationToken ct = default);
    Task SetChapterCoverByUrl(Chapter chapter, string url, bool fromBase64 = true, bool chooseBetterImage = false, CancellationToken ct = default);
    Task SetUserCoverByUrl(int userId, string url, bool fromBase64 = true, bool chooseBetterImage = false, CancellationToken ct = default);
    Task SetUserCoverByUrl(AppUser user, string url, bool fromBase64 = true, bool chooseBetterImage = false, CancellationToken ct = default);
}
