using System.Threading.Tasks;
using Kavita.Models.Entities;
using Kavita.Models.Entities.Enums;
using Kavita.Models.Entities.Person;
using Kavita.Models.Entities.User;

namespace Kavita.API.Services.Metadata;

public interface ICoverDbService
{
    Task<string> DownloadFaviconAsync(string url, EncodeFormat encodeFormat);
    Task<string> DownloadPublisherImageAsync(string publisherName, EncodeFormat encodeFormat);
    Task<string?> DownloadPersonImageAsync(Person person, EncodeFormat encodeFormat);
    Task<string?> DownloadPersonImageAsync(Person person, EncodeFormat encodeFormat, string url);
    Task SetPersonCoverByUrl(Person person, string url, bool fromBase64 = true, bool checkNoImagePlaceholder = false, bool chooseBetterImage = true);
    Task SetSeriesCoverByUrl(Series series, string url, bool fromBase64 = true, bool chooseBetterImage = false);
    Task SetChapterCoverByUrl(Chapter chapter, string url, bool fromBase64 = true, bool chooseBetterImage = false);
    Task SetUserCoverByUrl(int userId, string url, bool fromBase64 = true, bool chooseBetterImage = false);
    Task SetUserCoverByUrl(AppUser user, string url, bool fromBase64 = true, bool chooseBetterImage = false);
}
