using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using API.Constants;
using API.DTOs;
using API.DTOs.Account;
using API.DTOs.Dashboard;
using API.DTOs.Filtering.v2;
using API.DTOs.KavitaPlus.Account;
using API.DTOs.Reader;
using API.DTOs.Scrobbling;
using API.DTOs.SeriesDetail;
using API.DTOs.SideNav;
using API.Entities;
using API.Extensions;
using API.Extensions.QueryExtensions;
using API.Extensions.QueryExtensions.Filtering;
using API.Helpers;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace API.Data.Repositories;

[Flags]
public enum AppUserIncludes
{
    None = 1,
    Progress = 2,
    Bookmarks = 4,
    ReadingLists = 8,
    Ratings = 16,
    UserPreferences = 32,
    WantToRead = 64,
    ReadingListsWithItems = 128,
    Devices = 256,
    ScrobbleHolds = 512,
    SmartFilters = 1024,
    DashboardStreams = 2048,
    SideNavStreams = 4096,
    ExternalSources = 8192,
    Collections = 16384 // 2^14
}

public interface IUserRepository
{
    void Add(AppUserBookmark bookmark);
    void Add(AppUser bookmark);
    void Update(AppUser user);
    void Update(AppUserPreferences preferences);
    void Update(AppUserBookmark bookmark);
    void Update(AppUserDashboardStream stream);
    void Update(AppUserSideNavStream stream);
    void Delete(AppUser? user);
    void Delete(AppUserBookmark bookmark);
    void Delete(IEnumerable<AppUserDashboardStream> streams);
    void Delete(IEnumerable<AppUserSideNavStream> streams);
    Task<IEnumerable<MemberDto>> GetEmailConfirmedMemberDtosAsync(bool emailConfirmed = true);
    Task<IEnumerable<AppUser>> GetAdminUsersAsync();
    Task<bool> IsUserAdminAsync(AppUser? user);
    Task<IList<string>> GetRoles(int userId);
    Task<AppUserRating?> GetUserRatingAsync(int seriesId, int userId);
    Task<IList<UserReviewDto>> GetUserRatingDtosForSeriesAsync(int seriesId, int userId);
    Task<AppUserPreferences?> GetPreferencesAsync(string username);
    Task<IEnumerable<BookmarkDto>> GetBookmarkDtosForSeries(int userId, int seriesId);
    Task<IEnumerable<BookmarkDto>> GetBookmarkDtosForVolume(int userId, int volumeId);
    Task<IEnumerable<BookmarkDto>> GetBookmarkDtosForChapter(int userId, int chapterId);
    Task<IEnumerable<BookmarkDto>> GetAllBookmarkDtos(int userId, FilterV2Dto filter);
    Task<IEnumerable<AppUserBookmark>> GetAllBookmarksAsync();
    Task<AppUserBookmark?> GetBookmarkForPage(int page, int chapterId, int userId);
    Task<AppUserBookmark?> GetBookmarkAsync(int bookmarkId);
    Task<int> GetUserIdByApiKeyAsync(string apiKey);
    Task<AppUser?> GetUserByUsernameAsync(string username, AppUserIncludes includeFlags = AppUserIncludes.None);
    Task<AppUser?> GetUserByIdAsync(int userId, AppUserIncludes includeFlags = AppUserIncludes.None);
    Task<int> GetUserIdByUsernameAsync(string username);
    Task<IList<AppUserBookmark>> GetAllBookmarksByIds(IList<int> bookmarkIds);
    Task<AppUser?> GetUserByEmailAsync(string email, AppUserIncludes includes = AppUserIncludes.None);
    Task<IEnumerable<AppUserPreferences>> GetAllPreferencesByThemeAsync(int themeId);
    Task<bool> HasAccessToLibrary(int libraryId, int userId);
    Task<bool> HasAccessToSeries(int userId, int seriesId);
    Task<IEnumerable<AppUser>> GetAllUsersAsync(AppUserIncludes includeFlags = AppUserIncludes.None, bool track = true);
    Task<AppUser?> GetUserByConfirmationToken(string token);
    Task<AppUser> GetDefaultAdminUser(AppUserIncludes includes = AppUserIncludes.None);
    Task<IEnumerable<AppUserRating>> GetSeriesWithRatings(int userId);
    Task<IEnumerable<AppUserRating>> GetSeriesWithReviews(int userId);
    Task<bool> HasHoldOnSeries(int userId, int seriesId);
    Task<IList<ScrobbleHoldDto>> GetHolds(int userId);
    Task<string> GetLocale(int userId);
    Task<IList<DashboardStreamDto>> GetDashboardStreams(int userId, bool visibleOnly = false);
    Task<IList<AppUserDashboardStream>> GetAllDashboardStreams();
    Task<AppUserDashboardStream?> GetDashboardStream(int streamId);
    Task<IList<AppUserDashboardStream>> GetDashboardStreamWithFilter(int filterId);
    Task<IList<SideNavStreamDto>> GetSideNavStreams(int userId, bool visibleOnly = false);
    Task<AppUserSideNavStream?> GetSideNavStream(int streamId);
    Task<IList<AppUserSideNavStream>> GetSideNavStreamWithFilter(int filterId);
    Task<IList<AppUserSideNavStream>> GetSideNavStreamsByLibraryId(int libraryId);
    Task<IList<AppUserSideNavStream>> GetSideNavStreamWithExternalSource(int externalSourceId);
    Task<IList<AppUserSideNavStream>> GetDashboardStreamsByIds(IList<int> streamIds);
    Task<IEnumerable<UserTokenInfo>> GetUserTokenInfo();
    Task<AppUser?> GetUserByDeviceEmail(string deviceEmail);
}

public class UserRepository : IUserRepository
{
    private readonly DataContext _context;
    private readonly UserManager<AppUser> _userManager;
    private readonly IMapper _mapper;

    public UserRepository(DataContext context, UserManager<AppUser> userManager, IMapper mapper)
    {
        _context = context;
        _userManager = userManager;
        _mapper = mapper;
    }

    public void Add(AppUserBookmark bookmark)
    {
        _context.AppUserBookmark.Add(bookmark);
    }

    public void Add(AppUser user)
    {
        _context.AppUser.Add(user);
    }

    public void Update(AppUser user)
    {
        _context.Entry(user).State = EntityState.Modified;
    }

    public void Update(AppUserPreferences preferences)
    {
        _context.Entry(preferences).State = EntityState.Modified;
    }

    public void Update(AppUserBookmark bookmark)
    {
        _context.Entry(bookmark).State = EntityState.Modified;
    }

    public void Update(AppUserDashboardStream stream)
    {
        _context.Entry(stream).State = EntityState.Modified;
    }

    public void Update(AppUserSideNavStream stream)
    {
        _context.Entry(stream).State = EntityState.Modified;
    }

    public void Delete(AppUser? user)
    {
        if (user == null) return;
        _context.AppUser.Remove(user);
    }

    public void Delete(AppUserBookmark bookmark)
    {
        _context.AppUserBookmark.Remove(bookmark);
    }

    public void Delete(IEnumerable<AppUserDashboardStream> streams)
    {
        _context.AppUserDashboardStream.RemoveRange(streams);
    }

    public void Delete(IEnumerable<AppUserSideNavStream> streams)
    {
        _context.AppUserSideNavStream.RemoveRange(streams);
    }

    /// <summary>
    /// A one stop shop to get a tracked AppUser instance with any number of JOINs generated by passing bitwise flags.
    /// </summary>
    /// <param name="username"></param>
    /// <param name="includeFlags">Includes() you want. Pass multiple with flag1 | flag2 </param>
    /// <returns></returns>
    public async Task<AppUser?> GetUserByUsernameAsync(string username, AppUserIncludes includeFlags = AppUserIncludes.None)
    {
        return await _context.Users
            .Where(x => x.UserName == username)
            .Includes(includeFlags)
            .SingleOrDefaultAsync();
    }

    /// <summary>
    /// A one stop shop to get a tracked AppUser instance with any number of JOINs generated by passing bitwise flags.
    /// </summary>
    /// <param name="userId"></param>
    /// <param name="includeFlags">Includes() you want. Pass multiple with flag1 | flag2 </param>
    /// <returns></returns>
    public async Task<AppUser?> GetUserByIdAsync(int userId, AppUserIncludes includeFlags = AppUserIncludes.None)
    {
        return await _context.Users
            .Where(x => x.Id == userId)
            .Includes(includeFlags)
            .FirstOrDefaultAsync();
    }

    public async Task<IEnumerable<AppUserBookmark>> GetAllBookmarksAsync()
    {
        return await _context.AppUserBookmark.ToListAsync();
    }

    public async Task<AppUserBookmark?> GetBookmarkForPage(int page, int chapterId, int userId)
    {
        return await _context.AppUserBookmark
            .Where(b => b.Page == page && b.ChapterId == chapterId && b.AppUserId == userId)
            .SingleOrDefaultAsync();
    }

    public async Task<AppUserBookmark?> GetBookmarkAsync(int bookmarkId)
    {
        return await _context.AppUserBookmark
            .Where(b => b.Id == bookmarkId)
            .SingleOrDefaultAsync();
    }


    /// <summary>
    /// This fetches the Id for a user. Use whenever you just need an ID.
    /// </summary>
    /// <param name="username"></param>
    /// <returns></returns>
    public async Task<int> GetUserIdByUsernameAsync(string username)
    {
        return await _context.Users
            .Where(x => x.UserName == username)
            .Select(u => u.Id)
            .SingleOrDefaultAsync();
    }


    /// <summary>
    /// Returns all Bookmarks for a given set of Ids
    /// </summary>
    /// <param name="bookmarkIds"></param>
    /// <returns></returns>
    public async Task<IList<AppUserBookmark>> GetAllBookmarksByIds(IList<int> bookmarkIds)
    {
        return await _context.AppUserBookmark
            .Where(b => bookmarkIds.Contains(b.Id))
            .OrderBy(b => b.Created)
            .ToListAsync();
    }

    public async Task<AppUser?> GetUserByEmailAsync(string email, AppUserIncludes includes = AppUserIncludes.None)
    {
        var lowerEmail = email.ToLower();
        return await _context.AppUser
            .Includes(includes)
            .FirstOrDefaultAsync(u => u.Email != null && u.Email.ToLower().Equals(lowerEmail));
    }


    public async Task<IEnumerable<AppUserPreferences>> GetAllPreferencesByThemeAsync(int themeId)
    {
        return await _context.AppUserPreferences
            .Include(p => p.Theme)
            .Where(p => p.Theme.Id == themeId)
            .AsSplitQuery()
            .ToListAsync();
    }

    public async Task<bool> HasAccessToLibrary(int libraryId, int userId)
    {
        return await _context.Library
            .Include(l => l.AppUsers)
            .AsSplitQuery()
            .AnyAsync(library => library.AppUsers.Any(user => user.Id == userId) && library.Id == libraryId);
    }

    /// <summary>
    /// Does the user have library and age restriction access to a given series
    /// </summary>
    /// <returns></returns>
    public async Task<bool> HasAccessToSeries(int userId, int seriesId)
    {
        var userRating = await _context.AppUser.GetUserAgeRestriction(userId);
        return await _context.Series
            .Include(s => s.Library)
            .Where(s => s.Library.AppUsers.Any(user => user.Id == userId))
            .RestrictAgainstAgeRestriction(userRating)
            .AsSplitQuery()
            .AnyAsync(s => s.Id == seriesId);
    }

    public async Task<IEnumerable<AppUser>> GetAllUsersAsync(AppUserIncludes includeFlags = AppUserIncludes.None, bool track = true)
    {
        var query = _context.AppUser
            .Includes(includeFlags);
        if (track)
        {
            return await query.ToListAsync();
        }

        return await query
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task<AppUser?> GetUserByConfirmationToken(string token)
    {
        return await _context.AppUser
            .SingleOrDefaultAsync(u => u.ConfirmationToken != null && u.ConfirmationToken.Equals(token));
    }

    /// <summary>
    /// Returns the first admin account created
    /// </summary>
    /// <returns></returns>
    public async Task<AppUser> GetDefaultAdminUser(AppUserIncludes includes = AppUserIncludes.None)
    {
        return await _context.AppUser
            .Includes(includes)
            .Where(u => u.UserRoles.Any(r => r.Role.Name == PolicyConstants.AdminRole))
            .OrderBy(u => u.Created)
            .FirstAsync();
    }

    public async Task<IEnumerable<AppUserRating>> GetSeriesWithRatings(int userId)
    {
        return await _context.AppUserRating
            .Where(u => u.AppUserId == userId && u.Rating > 0)
            .Include(u => u.Series)
            .AsSplitQuery()
            .ToListAsync();
    }

    public async Task<IEnumerable<AppUserRating>> GetSeriesWithReviews(int userId)
    {
        return await _context.AppUserRating
            .Where(u => u.AppUserId == userId && !string.IsNullOrEmpty(u.Review))
            .Include(u => u.Series)
            .AsSplitQuery()
            .ToListAsync();
    }

    public async Task<bool> HasHoldOnSeries(int userId, int seriesId)
    {
        return await _context.AppUser
            .AsSplitQuery()
            .AnyAsync(u => u.ScrobbleHolds.Select(s => s.SeriesId).Contains(seriesId) && u.Id == userId);
    }

    public async Task<IList<ScrobbleHoldDto>> GetHolds(int userId)
    {
        return await _context.ScrobbleHold
            .Where(s => s.AppUserId == userId)
            .ProjectTo<ScrobbleHoldDto>(_mapper.ConfigurationProvider)
            .ToListAsync();
    }

    public async Task<string> GetLocale(int userId)
    {
        return await _context.AppUserPreferences.Where(p => p.AppUserId == userId)
            .Select(p => p.Locale)
            .SingleAsync();
    }

    public async Task<IList<DashboardStreamDto>> GetDashboardStreams(int userId, bool visibleOnly = false)
    {
        return await _context.AppUserDashboardStream
            .Where(d => d.AppUserId == userId)
            .WhereIf(visibleOnly, d => d.Visible)
            .OrderBy(d => d.Order)
            .Include(d => d.SmartFilter)
            .Select(d => new DashboardStreamDto()
            {
                Id = d.Id,
                Name = d.Name,
                IsProvided = d.IsProvided,
                SmartFilterId = d.SmartFilter == null ? 0 : d.SmartFilter.Id,
                SmartFilterEncoded = d.SmartFilter == null ? null : d.SmartFilter.Filter,
                StreamType = d.StreamType,
                Order = d.Order,
                Visible = d.Visible
            })
            .ToListAsync();
    }

    public async Task<IList<AppUserDashboardStream>> GetAllDashboardStreams()
    {
        return await _context.AppUserDashboardStream
            .OrderBy(d => d.Order)
            .ToListAsync();
    }

    public async Task<AppUserDashboardStream?> GetDashboardStream(int streamId)
    {
        return await _context.AppUserDashboardStream
            .Include(d => d.SmartFilter)
            .FirstOrDefaultAsync(d => d.Id == streamId);
    }

    public async Task<IList<AppUserDashboardStream>> GetDashboardStreamWithFilter(int filterId)
    {
        return await _context.AppUserDashboardStream
            .Include(d => d.SmartFilter)
            .Where(d => d.SmartFilter != null && d.SmartFilter.Id == filterId)
            .AsSplitQuery()
            .ToListAsync();
    }

    public async Task<IList<SideNavStreamDto>> GetSideNavStreams(int userId, bool visibleOnly = false)
    {
        var sideNavStreams = await _context.AppUserSideNavStream
            .Where(d => d.AppUserId == userId)
            .WhereIf(visibleOnly, d => d.Visible)
            .OrderBy(d => d.Order)
            .Include(d => d.SmartFilter)
            .Select(d => new SideNavStreamDto()
            {
                Id = d.Id,
                Name = d.Name,
                IsProvided = d.IsProvided,
                SmartFilterId = d.SmartFilter == null ? 0 : d.SmartFilter.Id,
                SmartFilterEncoded = d.SmartFilter == null ? null : d.SmartFilter.Filter,
                LibraryId = d.LibraryId ?? 0,
                ExternalSourceId = d.ExternalSourceId ?? 0,
                StreamType = d.StreamType,
                Order = d.Order,
                Visible = d.Visible
            })
            .AsSplitQuery()
            .ToListAsync();

        var libraryIds = sideNavStreams.Where(d => d.StreamType == SideNavStreamType.Library)
            .Select(d => d.LibraryId)
            .ToList();

        var libraryDtos = _context.Library
            .Where(l => libraryIds.Contains(l.Id))
            .ProjectTo<LibraryDto>(_mapper.ConfigurationProvider)
            .ToList();

        foreach (var dto in sideNavStreams.Where(dto => dto.StreamType == SideNavStreamType.Library))
        {
            dto.Library = libraryDtos.FirstOrDefault(l => l.Id == dto.LibraryId);
        }

        var externalSourceIds = sideNavStreams.Where(d => d.StreamType == SideNavStreamType.ExternalSource)
            .Select(d => d.ExternalSourceId)
            .ToList();

        var externalSourceDtos = _context.AppUserExternalSource
            .Where(l => externalSourceIds.Contains(l.Id))
            .ProjectTo<ExternalSourceDto>(_mapper.ConfigurationProvider)
            .ToList();

        foreach (var dto in sideNavStreams.Where(dto => dto.StreamType == SideNavStreamType.ExternalSource))
        {
            dto.ExternalSource = externalSourceDtos.FirstOrDefault(l => l.Id == dto.ExternalSourceId);
        }

        return sideNavStreams;
    }

    public async Task<AppUserSideNavStream> GetSideNavStream(int streamId)
    {
        return await _context.AppUserSideNavStream
            .Include(d => d.SmartFilter)
            .FirstOrDefaultAsync(d => d.Id == streamId);
    }

    public async Task<IList<AppUserSideNavStream>> GetSideNavStreamWithFilter(int filterId)
    {
        return await _context.AppUserSideNavStream
            .Include(d => d.SmartFilter)
            .Where(d => d.SmartFilter != null && d.SmartFilter.Id == filterId)
            .ToListAsync();
    }

    public async Task<IList<AppUserSideNavStream>> GetSideNavStreamsByLibraryId(int libraryId)
    {
        return await _context.AppUserSideNavStream
            .Where(d => d.LibraryId == libraryId)
            .ToListAsync();
    }

    public async Task<IList<AppUserSideNavStream>> GetSideNavStreamWithExternalSource(int externalSourceId)
    {
        return await _context.AppUserSideNavStream
            .Where(d => d.ExternalSourceId == externalSourceId)
            .ToListAsync();
    }

    public async Task<IList<AppUserSideNavStream>> GetDashboardStreamsByIds(IList<int> streamIds)
    {
        return await _context.AppUserSideNavStream
            .Where(d => streamIds.Contains(d.Id))
            .ToListAsync();
    }

    public async Task<IEnumerable<UserTokenInfo>> GetUserTokenInfo()
    {
        var users = await _context.AppUser
            .Select(u => new
            {
                u.Id,
                u.UserName,
                u.AniListAccessToken, // JWT Token
                u.MalAccessToken // JWT Token
            })
            .ToListAsync();

        var userTokenInfos = users.Select(user => new UserTokenInfo
        {
            UserId = user.Id,
            Username = user.UserName,
            IsAniListTokenSet = !string.IsNullOrEmpty(user.AniListAccessToken),
            AniListValidUntilUtc = JwtHelper.GetTokenExpiry(user.AniListAccessToken),
            IsAniListTokenValid = JwtHelper.IsTokenValid(user.AniListAccessToken),
            IsMalTokenSet = !string.IsNullOrEmpty(user.MalAccessToken),
        });

        return userTokenInfos;
    }

    /// <summary>
    /// Returns the first user with a device email matching
    /// </summary>
    /// <param name="deviceEmail"></param>
    /// <returns></returns>
    public async Task<AppUser> GetUserByDeviceEmail(string deviceEmail)
    {
        return await _context.AppUser
            .Where(u => u.Devices.Any(d => d.EmailAddress == deviceEmail))
            .FirstOrDefaultAsync();
    }


    public async Task<IEnumerable<AppUser>> GetAdminUsersAsync()
    {
        return (await _userManager.GetUsersInRoleAsync(PolicyConstants.AdminRole)).OrderBy(u => u.CreatedUtc);
    }

    public async Task<bool> IsUserAdminAsync(AppUser? user)
    {
        if (user == null) return false;
        return await _userManager.IsInRoleAsync(user, PolicyConstants.AdminRole);
    }

    public async Task<IList<string>> GetRoles(int userId)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
        if (user == null || _userManager == null) return ArraySegment<string>.Empty; // userManager is null on Unit Tests only

        return await _userManager.GetRolesAsync(user);
    }

    public async Task<AppUserRating?> GetUserRatingAsync(int seriesId, int userId)
    {
        return await _context.AppUserRating
            .Where(r => r.SeriesId == seriesId && r.AppUserId == userId)
            .SingleOrDefaultAsync();
    }

    public async Task<IList<UserReviewDto>> GetUserRatingDtosForSeriesAsync(int seriesId, int userId)
    {
        return await _context.AppUserRating
            .Include(r => r.AppUser)
            .Where(r => r.SeriesId == seriesId)
            .Where(r => r.AppUser.UserPreferences.ShareReviews || r.AppUserId == userId)
            .OrderBy(r => r.AppUserId == userId)
            .ThenBy(r => r.Rating)
            .AsSplitQuery()
            .ProjectTo<UserReviewDto>(_mapper.ConfigurationProvider)
            .ToListAsync();
    }

    public async Task<AppUserPreferences?> GetPreferencesAsync(string username)
    {
        return await _context.AppUserPreferences
            .Include(p => p.AppUser)
            .Include(p => p.Theme)
            .AsSplitQuery()
            .SingleOrDefaultAsync(p => p.AppUser.UserName == username);
    }

    public async Task<IEnumerable<BookmarkDto>> GetBookmarkDtosForSeries(int userId, int seriesId)
    {
        return await _context.AppUserBookmark
            .Where(x => x.AppUserId == userId && x.SeriesId == seriesId)
            .OrderBy(x => x.Created)
            .AsNoTracking()
            .ProjectTo<BookmarkDto>(_mapper.ConfigurationProvider)
            .ToListAsync();
    }

    public async Task<IEnumerable<BookmarkDto>> GetBookmarkDtosForVolume(int userId, int volumeId)
    {
        return await _context.AppUserBookmark
            .Where(x => x.AppUserId == userId && x.VolumeId == volumeId)
            .OrderBy(x => x.Created)
            .AsNoTracking()
            .ProjectTo<BookmarkDto>(_mapper.ConfigurationProvider)
            .ToListAsync();
    }

    public async Task<IEnumerable<BookmarkDto>> GetBookmarkDtosForChapter(int userId, int chapterId)
    {
        return await _context.AppUserBookmark
            .Where(x => x.AppUserId == userId && x.ChapterId == chapterId)
            .OrderBy(x => x.Created)
            .AsNoTracking()
            .ProjectTo<BookmarkDto>(_mapper.ConfigurationProvider)
            .ToListAsync();
    }

    /// <summary>
    /// Get all bookmarks for the user
    /// </summary>
    /// <param name="userId"></param>
    /// <param name="filter">Only supports SeriesNameQuery</param>
    /// <returns></returns>
    public async Task<IEnumerable<BookmarkDto>> GetAllBookmarkDtos(int userId, FilterV2Dto filter)
    {
        var query = _context.AppUserBookmark
            .Where(x => x.AppUserId == userId)
            .OrderBy(x => x.Created)
            .AsNoTracking();

        var filterSeriesQuery = query.Join(_context.Series, b => b.SeriesId, s => s.Id,
            (bookmark, series) => new BookmarkSeriesPair()
            {
                Bookmark = bookmark,
                Series = series
            });

        var filterStatement = filter.Statements.FirstOrDefault(f => f.Field == FilterField.SeriesName);
        if (filterStatement == null || string.IsNullOrWhiteSpace(filterStatement.Value))
        {
            return await ApplyLimit(filterSeriesQuery
                    .Sort(filter.SortOptions)
                    .AsSplitQuery(), filter.LimitTo)
                .ProjectTo<BookmarkDto>(_mapper.ConfigurationProvider)
                .ToListAsync();
        }

        var queryString = filterStatement.Value.ToNormalized();
        switch (filterStatement.Comparison)
        {
            case FilterComparison.Equal:
                filterSeriesQuery = filterSeriesQuery.Where(s => s.Series.Name.Equals(queryString)
                                                               || s.Series.OriginalName.Equals(queryString)
                                                               || s.Series.LocalizedName.Equals(queryString)
                                                               || s.Series.SortName.Equals(queryString));
                break;
            case FilterComparison.BeginsWith:
                filterSeriesQuery = filterSeriesQuery.Where(s => EF.Functions.Like(s.Series.Name, $"{queryString}%")
                                                                 ||EF.Functions.Like(s.Series.OriginalName, $"{queryString}%")
                                                                 || EF.Functions.Like(s.Series.LocalizedName, $"{queryString}%")
                                                                 || EF.Functions.Like(s.Series.SortName, $"{queryString}%"));
                break;
            case FilterComparison.EndsWith:
                filterSeriesQuery = filterSeriesQuery.Where(s => EF.Functions.Like(s.Series.Name, $"%{queryString}")
                                                                 ||EF.Functions.Like(s.Series.OriginalName, $"%{queryString}")
                                                                 || EF.Functions.Like(s.Series.LocalizedName, $"%{queryString}")
                                                                 || EF.Functions.Like(s.Series.SortName, $"%{queryString}"));
                break;
            case FilterComparison.Matches:
                filterSeriesQuery = filterSeriesQuery.Where(s => EF.Functions.Like(s.Series.Name, $"%{queryString}%")
                                                                 ||EF.Functions.Like(s.Series.OriginalName, $"%{queryString}%")
                                                                 || EF.Functions.Like(s.Series.LocalizedName, $"%{queryString}%")
                                                                 || EF.Functions.Like(s.Series.SortName, $"%{queryString}%"));
                break;
            case FilterComparison.NotEqual:
                filterSeriesQuery = filterSeriesQuery.Where(s => s.Series.Name != queryString
                                                                 || s.Series.OriginalName != queryString
                                                                 || s.Series.LocalizedName != queryString
                                                                 || s.Series.SortName != queryString);
                break;
            case FilterComparison.MustContains:
            case FilterComparison.NotContains:
            case FilterComparison.GreaterThan:
            case FilterComparison.GreaterThanEqual:
            case FilterComparison.LessThan:
            case FilterComparison.LessThanEqual:
            case FilterComparison.Contains:
            case FilterComparison.IsBefore:
            case FilterComparison.IsAfter:
            case FilterComparison.IsInLast:
            case FilterComparison.IsNotInLast:
            default:
                break;
        }

        return await ApplyLimit(filterSeriesQuery
                .Sort(filter.SortOptions)
                .AsSplitQuery(), filter.LimitTo)
            .ProjectTo<BookmarkDto>(_mapper.ConfigurationProvider)
            .ToListAsync();
    }

    private static IQueryable<BookmarkSeriesPair> ApplyLimit(IQueryable<BookmarkSeriesPair> query, int limit)
    {
        return limit <= 0 ? query : query.Take(limit);
    }


    /// <summary>
    /// Fetches the UserId by API Key. This does not include any extra information
    /// </summary>
    /// <param name="apiKey"></param>
    /// <returns></returns>
    public async Task<int> GetUserIdByApiKeyAsync(string apiKey)
    {
        return await _context.AppUser
            .Where(u => u.ApiKey != null && u.ApiKey.Equals(apiKey))
            .Select(u => u.Id)
            .FirstOrDefaultAsync();
    }


    public async Task<IEnumerable<MemberDto>> GetEmailConfirmedMemberDtosAsync(bool emailConfirmed = true)
    {
        return await _context.Users
            .Where(u => (emailConfirmed && u.EmailConfirmed) || !emailConfirmed)
            .Include(x => x.Libraries)
            .Include(r => r.UserRoles)
            .ThenInclude(r => r.Role)
            .OrderBy(u => u.UserName)
            .Select(u => new MemberDto
            {
                Id = u.Id,
                Username = u.UserName,
                Email = u.Email,
                Created = u.Created,
                CreatedUtc = u.CreatedUtc,
                LastActive = u.LastActive,
                LastActiveUtc = u.LastActiveUtc,
                Roles = u.UserRoles.Select(r => r.Role.Name).ToList(),
                IsPending = !u.EmailConfirmed,
                AgeRestriction = new AgeRestrictionDto()
                {
                    AgeRating = u.AgeRestriction,
                    IncludeUnknowns = u.AgeRestrictionIncludeUnknowns
                },
                Libraries =  u.Libraries.Select(l => new LibraryDto
                {
                    Name = l.Name,
                    Type = l.Type,
                    LastScanned = l.LastScanned,
                    Folders = l.Folders.Select(x => x.Path).ToList()
                }).ToList()
            })
            .AsSplitQuery()
            .AsNoTracking()
            .ToListAsync();
    }
}
