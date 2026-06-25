using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Kavita.Models.DTOs;
using Kavita.Models.Entities.User;

namespace Kavita.API.Repositories;

public interface IAppUserReadingProfileRepository
{
    /// <summary>
    /// Returns the reading profile to use for the given series
    /// </summary>
    /// <param name="userId"></param>
    /// <param name="libraryId"></param>
    /// <param name="seriesId"></param>
    /// <param name="activeDeviceId"></param>
    /// <param name="skipImplicit"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    Task<AppUserReadingProfile> GetProfileForSeries(int userId, int libraryId, int seriesId, int? activeDeviceId = null, bool skipImplicit = false, CancellationToken ct = default);

    /// <summary>
    /// Get all profiles assigned to a library
    /// </summary>
    /// <param name="userId"></param>
    /// <param name="libraryId"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    Task<List<AppUserReadingProfile>> GetProfilesForLibrary(int userId, int libraryId, CancellationToken ct = default);

    /// <summary>
    /// Returns all reading profiles that currently select the given font family
    /// </summary>
    /// <param name="family"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    Task<IList<AppUserReadingProfile>> GetProfilesByFontFamily(string family, CancellationToken ct = default);

    /// <summary>
    /// Return the profile if it belongs to the user
    /// </summary>
    /// <param name="userId"></param>
    /// <param name="profileId"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    Task<AppUserReadingProfile?> GetUserProfile(int userId, int profileId, CancellationToken ct = default);

    /// <summary>
    /// Returns all reading profiles for the user
    /// </summary>
    /// <param name="userId"></param>
    /// <param name="skipImplicit"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    Task<IList<AppUserReadingProfile>> GetProfilesForUser(int userId, bool skipImplicit = false, CancellationToken ct = default);

    /// <summary>
    /// Returns all reading profiles for the user
    /// </summary>
    /// <param name="userId"></param>
    /// <param name="skipImplicit"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    Task<IList<UserReadingProfileDto>> GetProfilesDtoForUser(int userId, bool skipImplicit = false, CancellationToken ct = default);

    /// <summary>
    /// Is there a user reading profile with this name (normalized)?
    /// </summary>
    /// <param name="userId"></param>
    /// <param name="name"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    Task<bool> IsProfileNameInUse(int userId, string name, CancellationToken ct = default);

    void Add(AppUserReadingProfile readingProfile);
    void Update(AppUserReadingProfile readingProfile);
    void Remove(AppUserReadingProfile readingProfile);
    void RemoveRange(IEnumerable<AppUserReadingProfile> readingProfiles);
}
