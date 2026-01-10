#nullable enable
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using API.DTOs;
using API.Entities;
using API.Entities.Enums;
using API.Extensions;
using API.Extensions.QueryExtensions;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using Microsoft.EntityFrameworkCore;

namespace API.Data.Repositories;


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
    /// <returns></returns>
    Task<AppUserReadingProfile> GetProfileForSeries(int userId, int libraryId, int seriesId, int? activeDeviceId = null, bool skipImplicit = false);

    /// <summary>
    /// Get all profiles assigned to a library
    /// </summary>
    /// <param name="userId"></param>
    /// <param name="libraryId"></param>
    /// <returns></returns>
    Task<List<AppUserReadingProfile>> GetProfilesForLibrary(int userId, int libraryId);
    /// <summary>
    /// Return the profile if it belongs the user
    /// </summary>
    /// <param name="userId"></param>
    /// <param name="profileId"></param>
    /// <returns></returns>
    Task<AppUserReadingProfile?> GetUserProfile(int userId, int profileId);

    /// <summary>
    /// Returns all reading profiles for the user
    /// </summary>
    /// <param name="userId"></param>
    /// <param name="skipImplicit"></param>
    /// <returns></returns>
    Task<IList<AppUserReadingProfile>> GetProfilesForUser(int userId, bool skipImplicit = false);

    /// <summary>
    /// Returns all reading profiles for the user
    /// </summary>
    /// <param name="userId"></param>
    /// <param name="skipImplicit"></param>
    /// <returns></returns>
    Task<IList<UserReadingProfileDto>> GetProfilesDtoForUser(int userId, bool skipImplicit = false);
    /// <summary>
    /// Is there a user reading profile with this name (normalized)
    /// </summary>
    /// <param name="userId"></param>
    /// <param name="name"></param>
    /// <returns></returns>
    Task<bool> IsProfileNameInUse(int userId, string name);

    void Add(AppUserReadingProfile readingProfile);
    void Update(AppUserReadingProfile readingProfile);
    void Remove(AppUserReadingProfile readingProfile);
    void RemoveRange(IEnumerable<AppUserReadingProfile> readingProfiles);
}

public class AppUserReadingProfileRepository(DataContext context, IMapper mapper): IAppUserReadingProfileRepository
{

    public Task<AppUserReadingProfile> GetProfileForSeries(int userId, int libraryId, int seriesId, int? activeDeviceId = null, bool skipImplicit = false)
    {
        return context.AppUserReadingProfiles
            .Where(rp => rp.AppUserId == userId)
            .WhereIf(skipImplicit, rp => rp.Kind != ReadingProfileKind.Implicit)
            .Where(rp => rp.DeviceIds.Count == 0 || activeDeviceId == null || rp.DeviceIds.Contains(activeDeviceId.Value))
            .OrderByDescending(rp => rp.Kind == ReadingProfileKind.Implicit && rp.SeriesIds.Contains(seriesId) && (rp.DeviceIds.Count == 0 || (activeDeviceId != null && rp.DeviceIds.Contains(activeDeviceId.Value))))
            .ThenByDescending(rp => rp.Kind == ReadingProfileKind.Implicit && rp.SeriesIds.Contains(seriesId))
            .ThenByDescending(rp => rp.SeriesIds.Contains(seriesId) && (rp.DeviceIds.Count == 0 || (activeDeviceId != null && rp.DeviceIds.Contains(activeDeviceId.Value))))
            .ThenByDescending(rp => rp.SeriesIds.Contains(seriesId))
            .ThenByDescending(rp => rp.LibraryIds.Contains(libraryId) && (rp.DeviceIds.Count == 0 || (activeDeviceId != null && rp.DeviceIds.Contains(activeDeviceId.Value))))
            .ThenByDescending(rp => rp.LibraryIds.Contains(libraryId))
            .ThenByDescending(rp => rp.Kind == ReadingProfileKind.Default)
            .FirstAsync();
    }

    public Task<List<AppUserReadingProfile>> GetProfilesForLibrary(int userId, int libraryId)
    {
        return context.AppUserReadingProfiles
            .Where(rp => rp.AppUserId == userId && rp.LibraryIds.Contains(libraryId))
            .ToListAsync();
    }

    public async Task<AppUserReadingProfile?> GetUserProfile(int userId, int profileId)
    {
        return await context.AppUserReadingProfiles
            .Where(rp => rp.AppUserId == userId && rp.Id == profileId)
            .FirstOrDefaultAsync();
    }

    public async Task<IList<AppUserReadingProfile>> GetProfilesForUser(int userId, bool skipImplicit = false)
    {
        return await context.AppUserReadingProfiles
            .Where(rp => rp.AppUserId == userId)
            .WhereIf(skipImplicit, rp => rp.Kind != ReadingProfileKind.Implicit)
            .ToListAsync();
    }

    /// <summary>
    /// Returns all Reading Profiles for the User
    /// </summary>
    /// <param name="userId"></param>
    /// <returns></returns>
    public async Task<IList<UserReadingProfileDto>> GetProfilesDtoForUser(int userId, bool skipImplicit = false)
    {
        return await context.AppUserReadingProfiles
            .Where(rp => rp.AppUserId == userId)
            .WhereIf(skipImplicit, rp => rp.Kind != ReadingProfileKind.Implicit)
            .ProjectTo<UserReadingProfileDto>(mapper.ConfigurationProvider)
            .ToListAsync();
    }

    public async Task<bool> IsProfileNameInUse(int userId, string name)
    {
        var normalizedName = name.ToNormalized();

        return await context.AppUserReadingProfiles
            .Where(rp => rp.NormalizedName == normalizedName && rp.AppUserId == userId)
            .AnyAsync();
    }

    public void Add(AppUserReadingProfile readingProfile)
    {
        context.AppUserReadingProfiles.Add(readingProfile);
    }

    public void Update(AppUserReadingProfile readingProfile)
    {
        context.AppUserReadingProfiles.Update(readingProfile).State = EntityState.Modified;
    }

    public void Remove(AppUserReadingProfile readingProfile)
    {
        context.AppUserReadingProfiles.Remove(readingProfile);
    }

    public void RemoveRange(IEnumerable<AppUserReadingProfile> readingProfiles)
    {
        context.AppUserReadingProfiles.RemoveRange(readingProfiles);
    }
}
