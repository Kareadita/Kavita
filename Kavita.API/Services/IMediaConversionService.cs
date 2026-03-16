using System.Threading;
using System.Threading.Tasks;
using Hangfire;
using Kavita.Models.Entities.Enums;

namespace Kavita.API.Services;

public interface IMediaConversionService
{
    [DisableConcurrentExecution(timeoutInSeconds: 2 * 60 * 60), AutomaticRetry(Attempts = 0)]
    Task ConvertAllBookmarkToEncoding(CancellationToken ct = default);
    [DisableConcurrentExecution(timeoutInSeconds: 2 * 60 * 60), AutomaticRetry(Attempts = 0)]
    Task ConvertAllCoversToEncoding(CancellationToken ct = default);
    [DisableConcurrentExecution(timeoutInSeconds: 2 * 60 * 60), AutomaticRetry(Attempts = 0)]
    Task ConvertAllManagedMediaToEncodingFormat(CancellationToken ct = default);

    Task<string> SaveAsEncodingFormat(string imageDirectory, string filename, string targetFolder,
        EncodeFormat encodeFormat);
}
