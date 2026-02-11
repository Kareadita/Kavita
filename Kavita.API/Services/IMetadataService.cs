using System.Threading.Tasks;
using Hangfire;
using Kavita.Models.DTOs.Settings;
using Kavita.Models.Entities;
using Kavita.Models.Entities.Enums;

namespace Kavita.API.Services;

public interface IMetadataService
{
    /// <summary>
    /// Recalculates cover images for all entities in a library.
    /// </summary>
    /// <param name="libraryId"></param>
    /// <param name="forceUpdate"></param>
    [DisableConcurrentExecution(timeoutInSeconds: 60 * 60 * 60)]
    [AutomaticRetry(Attempts = 3, OnAttemptsExceeded = AttemptsExceededAction.Delete)]
    Task GenerateCoversForLibrary(int libraryId, bool forceUpdate = false, bool forceColorScape = false);
    /// <summary>
    /// Performs a forced refresh of cover images just for a series, and it's nested entities
    /// </summary>
    /// <param name="libraryId"></param>
    /// <param name="seriesId"></param>
    /// <param name="forceUpdate">Overrides any cache logic and forces execution</param>

    Task GenerateCoversForSeries(ServerSettingDto serverSetting, int libraryId, int seriesId, bool forceUpdate = true, bool forceColorScape = true);
    Task GenerateCoversForSeries(Series series, EncodeFormat encodeFormat, CoverImageSize coverImageSize, bool forceUpdate = false, bool forceColorScape = true);
    Task RemoveAbandonedMetadataKeys();
}
