#nullable enable
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using API.Entities;
using AutoMapper;
using Microsoft.EntityFrameworkCore;

namespace API.Data.Repositories;

public interface IAppUserReadingProfileRepository
{
    Task<IList<AppUserReadingProfile>> GetProfilesForUser(int userId);
    Task<AppUserReadingProfile?> GetProfileForSeries(int userId, int seriesId);
    Task<AppUserReadingProfile?> GetProfileForLibrary(int userId, int libraryId);
    Task<AppUserReadingProfile?> GetProfile(int profileId);

    void Add(AppUserReadingProfile readingProfile);
    void Update(AppUserReadingProfile readingProfile);
    void Remove(AppUserReadingProfile readingProfile);
}

public class AppUserReadingProfileRepository(DataContext context, IMapper mapper): IAppUserReadingProfileRepository
{

    public async Task<IList<AppUserReadingProfile>> GetProfilesForUser(int userId)
    {
        return await context.AppUserReadingProfile
            .Where(rp => rp.UserId == userId)
            .ToListAsync();
    }

    public async Task<AppUserReadingProfile?> GetProfileForSeries(int userId, int seriesId)
    {
        return await context.AppUserReadingProfile
            .Where(rp => rp.UserId == userId && rp.Series.Any(s => s.Id == seriesId))
            .FirstOrDefaultAsync();
    }

    public async Task<AppUserReadingProfile?> GetProfileForLibrary(int userId, int libraryId)
    {
        return await context.AppUserReadingProfile
            .Where(rp => rp.UserId == userId && rp.Libraries.Any(s => s.Id == libraryId))
            .FirstOrDefaultAsync();
    }

    public async Task<AppUserReadingProfile?> GetProfile(int profileId)
    {
        return await context.AppUserReadingProfile
            .Where(rp => rp.Id == profileId)
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
