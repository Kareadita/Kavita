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
    /// Return the given profile if it belongs the user
    /// </summary>
    /// <param name="userId"></param>
    /// <param name="profileId"></param>
    /// <returns></returns>
    Task<AppUserReadingProfile?> GetUserProfile(int userId, int profileId);
    /// <summary>
    /// Returns all reading profiles for the user
    /// </summary>
    /// <param name="userId"></param>
    /// <returns></returns>
    Task<IList<AppUserReadingProfile>> GetProfilesForUser(int userId);
    /// <summary>
    /// Returns all non-implicit reading profiles for the user
    /// </summary>
    /// <param name="userId"></param>
    /// <returns></returns>
    Task<IList<UserReadingProfileDto>> GetProfilesDtoForUser(int userId);
    /// <summary>
    /// Find a profile by name, belonging to a specific user
    /// </summary>
    /// <param name="userId"></param>
    /// <param name="name"></param>
    /// <returns></returns>
    Task<AppUserReadingProfile?> GetProfileByName(int userId, string name);

    void Add(AppUserReadingProfile readingProfile);
    void Update(AppUserReadingProfile readingProfile);
    void Remove(AppUserReadingProfile readingProfile);
    void RemoveRange(IEnumerable<AppUserReadingProfile> readingProfiles);
}

public class AppUserReadingProfileRepository(DataContext context, IMapper mapper): IAppUserReadingProfileRepository
{
    public async Task<AppUserReadingProfile?> GetUserProfile(int userId, int profileId)
    {
        return await context.AppUserReadingProfiles
            .Where(rp => rp.AppUserId == userId && rp.Id == profileId)
            .FirstOrDefaultAsync();
    }

    public async Task<IList<AppUserReadingProfile>> GetProfilesForUser(int userId)
    {
        return await context.AppUserReadingProfiles
            .Where(rp => rp.AppUserId == userId)
            .ToListAsync();
    }

    public async Task<IList<UserReadingProfileDto>> GetProfilesDtoForUser(int userId)
    {
        return await context.AppUserReadingProfiles
            .Where(rp => rp.AppUserId == userId)
            .Where(rp => rp.Kind !=ReadingProfileKind.Implicit)
            .ProjectTo<UserReadingProfileDto>(mapper.ConfigurationProvider)
            .ToListAsync();
    }

    public async Task<AppUserReadingProfile?> GetProfileByName(int userId, string name)
    {
        var normalizedName = name.ToNormalized();

        return await context.AppUserReadingProfiles
            .Where(rp => rp.NormalizedName == normalizedName && rp.AppUserId == userId)
            .FirstOrDefaultAsync();
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
