#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using API.DTOs;
using API.Entities;
using API.Extensions;
using API.Extensions.QueryExtensions;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using Microsoft.EntityFrameworkCore;

namespace API.Data.Repositories;

[Flags]
public enum ReadingProfileIncludes
{
    None = 0,
    Series = 1 << 1,
    Library = 1 << 2
}

public interface IAppUserReadingProfileRepository
{
    Task<IList<AppUserReadingProfile>> GetProfilesForUser(int userId, bool nonImplicitOnly, ReadingProfileIncludes includes = ReadingProfileIncludes.None);
    Task<AppUserReadingProfile?> GetProfileForSeries(int userId, int seriesId, ReadingProfileIncludes includes = ReadingProfileIncludes.None);
    Task<UserReadingProfileDto?> GetProfileDtoForSeries(int userId, int seriesId);
    Task<AppUserReadingProfile?> GetProfileForLibrary(int userId, int libraryId, ReadingProfileIncludes includes = ReadingProfileIncludes.None);
    Task<UserReadingProfileDto?> GetProfileDtoForLibrary(int userId, int libraryId);
    Task<AppUserReadingProfile?> GetProfile(int profileId, ReadingProfileIncludes includes = ReadingProfileIncludes.None);
    Task<UserReadingProfileDto?> GetProfileDto(int profileId);
    Task<AppUserReadingProfile?> GetProfileByName(int userId, string name);

    void Add(AppUserReadingProfile readingProfile);
    void Update(AppUserReadingProfile readingProfile);
    void Remove(AppUserReadingProfile readingProfile);
}

public class AppUserReadingProfileRepository(DataContext context, IMapper mapper): IAppUserReadingProfileRepository
{

    public async Task<IList<AppUserReadingProfile>> GetProfilesForUser(int userId, bool nonImplicitOnly, ReadingProfileIncludes includes = ReadingProfileIncludes.None)
    {
        return await context.AppUserReadingProfile
            .Where(rp => rp.UserId == userId && !(nonImplicitOnly && rp.Implicit))
            .Includes(includes)
            .ToListAsync();
    }

    public async Task<AppUserReadingProfile?> GetProfileForSeries(int userId, int seriesId, ReadingProfileIncludes includes = ReadingProfileIncludes.None)
    {
        return await context.AppUserReadingProfile
            .Where(rp => rp.UserId == userId && rp.Series.Any(s => s.Id == seriesId))
            .Includes(includes)
            .OrderByDescending(rp => rp.Implicit) // Get implicit profiles first
            .FirstOrDefaultAsync();
    }

    public async Task<UserReadingProfileDto?> GetProfileDtoForSeries(int userId, int seriesId)
    {
        return await context.AppUserReadingProfile
            .Where(rp => rp.UserId == userId && rp.Series.Any(s => s.Id == seriesId))
            .OrderByDescending(rp => rp.Implicit) // Get implicit profiles first
            .ProjectTo<UserReadingProfileDto>(mapper.ConfigurationProvider)
            .FirstOrDefaultAsync();
    }

    public async Task<AppUserReadingProfile?> GetProfileForLibrary(int userId, int libraryId, ReadingProfileIncludes includes = ReadingProfileIncludes.None)
    {
        return await context.AppUserReadingProfile
            .Where(rp => rp.UserId == userId && rp.Libraries.Any(s => s.Id == libraryId))
            .Includes(includes)
            .FirstOrDefaultAsync();
    }

    public async Task<UserReadingProfileDto?> GetProfileDtoForLibrary(int userId, int libraryId)
    {
        return await context.AppUserReadingProfile
            .Where(rp => rp.UserId == userId && rp.Libraries.Any(s => s.Id == libraryId))
            .ProjectTo<UserReadingProfileDto>(mapper.ConfigurationProvider)
            .FirstOrDefaultAsync();
    }

    public async Task<AppUserReadingProfile?> GetProfile(int profileId, ReadingProfileIncludes includes = ReadingProfileIncludes.None)
    {
        return await context.AppUserReadingProfile
            .Where(rp => rp.Id == profileId)
            .Includes(includes)
            .FirstOrDefaultAsync();
    }
    public async Task<UserReadingProfileDto?> GetProfileDto(int profileId)
    {
        return await context.AppUserReadingProfile
            .Where(rp => rp.Id == profileId)
            .ProjectTo<UserReadingProfileDto>(mapper.ConfigurationProvider)
            .FirstOrDefaultAsync();
    }

    public async Task<AppUserReadingProfile?> GetProfileByName(int userId, string name)
    {
        var normalizedName = name.ToNormalized();

        return await context.AppUserReadingProfile
            .Where(rp => rp.NormalizedName == normalizedName && rp.UserId == userId)
            .FirstOrDefaultAsync();
    }

    public void Add(AppUserReadingProfile readingProfile)
    {
        context.AppUserReadingProfile.Add(readingProfile);
    }

    public void Update(AppUserReadingProfile readingProfile)
    {
        context.AppUserReadingProfile.Update(readingProfile).State = EntityState.Modified;
    }

    public void Remove(AppUserReadingProfile readingProfile)
    {
        context.AppUserReadingProfile.Remove(readingProfile);
    }
}
