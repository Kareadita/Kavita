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
using API.Entities.Enums.UserPreferences;
using API.Entities.User;
using API.Extensions;
using API.Extensions.QueryExtensions;
using API.Extensions.QueryExtensions.Filtering;
using API.Helpers;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace API.Data.Repositories;
#nullable enable

[Flags]
public enum AppUserIncludes
{
    None = 1,
    Progress = 1 << 1,
    Bookmarks = 1 << 2,
    ReadingLists = 1 << 3,
    Ratings = 1 << 4,
    UserPreferences = 1 << 5,
    WantToRead = 1 << 6,
    ReadingListsWithItems = 1 << 7,
    Devices = 1 << 8,
    ScrobbleHolds = 1 << 9,
    SmartFilters = 1 << 10,
    DashboardStreams = 1 << 11,
    SideNavStreams = 1 << 12,
    ExternalSources = 1 << 13,
    Collections = 1 << 14,
    ChapterRatings = 1 << 15,
    AuthKeys = 1 << 16
}

public interface IUserRepository
{
    void Add(AppUserAuthKey key);
    void Add(AppUserBookmark bookmark);
    void Add(AppUser bookmark);
    void Update(AppUser user);
    void Update(AppUserPreferences preferences);
    void Update(AppUserBookmark bookmark);
    void Update(AppUserDashboardStream stream);
    void Update(AppUserSideNavStream stream);
    void Delete(AppUser? user);
    void Delete(AppUserAuthKey? key);
    void Delete(AppUserBookmark bookmark);
    void Delete(IEnumerable<AppUserDashboardStream> streams);
    void Delete(AppUserDashboardStream stream);
    void Delete(IEnumerable<AppUserSideNavStream> streams);
    void Delete(AppUserSideNavStream stream);
    Task<IEnumerable<MemberDto>> GetEmailConfirmedMemberDtosAsync(bool emailConfirmed = true);
    Task<IEnumerable<AppUser>> GetAdminUsersAsync();
    Task<bool> IsUserAdminAsync(AppUser? user);
    Task<IList<string>> GetRoles(int userId);
    Task<IList<string>> GetRolesByAuthKey(string? apiKey);
    Task<AppUserRating?> GetUserRatingAsync(int seriesId, int userId);
    Task<AppUserChapterRating?> GetUserChapterRatingAsync(int userId, int chapterId);
    Task<IList<UserReviewDto>> GetUserRatingDtosForSeriesAsync(int seriesId, int userId);
    Task<IList<UserReviewDto>> GetUserRatingDtosForChapterAsync(int chapterId, int userId);
    Task<AppUserPreferences?> GetPreferencesAsync(string username);
    Task<IEnumerable<BookmarkDto>> GetBookmarkDtosForSeries(int userId, int seriesId);
    Task<IEnumerable<BookmarkDto>> GetBookmarkDtosForVolume(int userId, int volumeId);
    Task<IEnumerable<BookmarkDto>> GetBookmarkDtosForChapter(int userId, int chapterId);
    Task<IEnumerable<BookmarkDto>> GetAllBookmarkDtos(int userId, FilterV2Dto filter);
    Task<IEnumerable<AppUserBookmark>> GetAllBookmarksAsync();
    Task<AppUserBookmark?> GetBookmarkForPage(int page, int chapterId, int imageOffset, int userId);
    Task<AppUserBookmark?> GetBookmarkAsync(int bookmarkId);
    Task<UserDto?> GetUserDtoByAuthKeyAsync(string authKey);
    Task<int> GetUserIdByAuthKeyAsync(string authKey);
    Task<UserDto?> GetUserDtoById(int userId);
    Task<AppUser?> GetUserByUsernameAsync(string username, AppUserIncludes includeFlags = AppUserIncludes.None);
    Task<AppUser?> GetUserByIdAsync(int userId, AppUserIncludes includeFlags = AppUserIncludes.None);
    Task<AppUser?> GetUserByAuthKey(string authKey, AppUserIncludes includeFlags = AppUserIncludes.None);
    Task<int> GetUserIdByUsernameAsync(string username);
    Task<IList<AppUserBookmark>> GetAllBookmarksByIds(IList<int> bookmarkIds);
    Task<AppUser?> GetUserByEmailAsync(string email, AppUserIncludes includes = AppUserIncludes.None);
    Task<IEnumerable<AppUserPreferences>> GetAllPreferencesByThemeAsync(int themeId);
    Task<IEnumerable<AppUserPreferences>> GetAllPreferencesByFontAsync(string fontName);
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
    Task<AppUserSideNavStream?> GetSideNavStreamWithUser(int streamId);
    Task<IList<AppUserSideNavStream>> GetSideNavStreamWithFilter(int filterId);
    Task<IList<AppUserSideNavStream>> GetSideNavStreamsByLibraryId(int libraryId);
    Task<IList<AppUserSideNavStream>> GetSideNavStreamWithExternalSource(int externalSourceId);
    Task<IList<AppUserSideNavStream>> GetDashboardStreamsByIds(IList<int> streamIds);
    Task<IEnumerable<UserTokenInfo>> GetUserTokenInfo();
    Task<AppUser?> GetUserByDeviceEmail(string deviceEmail);
    Task<List<AnnotationDto>> GetAnnotations(int userId, int chapterId);
    Task<List<AnnotationDto>> GetAnnotationsByPage(int userId, int chapterId, int pageNum);
    /// <summary>
    /// Try getting a user by the id provided by OIDC
    /// </summary>
    /// <param name="oidcId"></param>
    /// <param name="includes"></param>
    /// <returns></returns>
    Task<AppUser?> GetByOidcId(string? oidcId, AppUserIncludes includes = AppUserIncludes.None);

    Task<AnnotationDto?> GetAnnotationDtoById(int userId, int annotationId);
    Task<List<AnnotationDto>> GetAnnotationDtosBySeries(int userId, int seriesId);
    Task UpdateUserAsActive(int userId);
    Task<IList<UserReviewExtendedDto>> GetAllReviewsForUser(int userId, int requestingUserId, string? query = null, float? ratingFilter = null);
    Task<string?> GetCoverImageAsync(int userId, int requestingUserId);
    Task<string?> GetPersonCoverImageAsync(int personId);
    Task<IList<AuthKeyDto>> GetAuthKeysForUserId(int userId);
    Task<IList<AuthKeyDto>> GetAllAuthKeysDtosWithExpiration();
    Task<AppUserAuthKey?> GetAuthKeyById(int authKeyId);
    Task<DateTime?> GetAuthKeyExpiration(string authKey, int userId);
    Task<AppUserSocialPreferences> GetSocialPreferencesForUser(int userId);
    Task<AppUserPreferences> GetPreferencesForUser(int userId);
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

    public void Add(AppUserAuthKey key)
    {
        _context.AppUserAuthKey.Add(key);
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

    public void Delete(AppUserAuthKey? key)
    {
        if (key == null) return;
        _context.AppUserAuthKey.Remove(key);
    }

    public void Delete(AppUserBookmark bookmark)
    {
        _context.AppUserBookmark.Remove(bookmark);
    }

    public void Delete(IEnumerable<AppUserDashboardStream> streams)
    {
        _context.AppUserDashboardStream.RemoveRange(streams);
    }

    public void Delete(AppUserDashboardStream stream)
    {
        _context.AppUserDashboardStream.Remove(stream);
    }

    public void Delete(IEnumerable<AppUserSideNavStream> streams)
    {
        _context.AppUserSideNavStream.RemoveRange(streams);
    }

    public void Delete(AppUserSideNavStream stream)
    {
        _context.AppUserSideNavStream.Remove(stream);
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

    public async Task<AppUser?> GetUserByAuthKey(string authKey, AppUserIncludes includeFlags = AppUserIncludes.None)
    {
        if (string.IsNullOrEmpty(authKey)) return null;

        return await _context.AppUserAuthKey
            .Where(ak => ak.Key == authKey)
            .HasNotExpired()
            .Select(ak => ak.AppUser)
            .Includes(includeFlags)
            .FirstOrDefaultAsync();
    }

    public async Task<IEnumerable<AppUserBookmark>> GetAllBookmarksAsync()
    {
        return await _context.AppUserBookmark.ToListAsync();
    }

    public async Task<AppUserBookmark?> GetBookmarkForPage(int page, int chapterId, int imageOffset, int userId)
    {
        return await _context.AppUserBookmark
            .Where(b => b.Page == page && b.ChapterId == chapterId && b.AppUserId == userId && b.ImageOffset == imageOffset)
            .FirstOrDefaultAsync();
    }

    public async Task<AppUserBookmark?> GetBookmarkAsync(int bookmarkId)
    {
        return await _context.AppUserBookmark
            .Where(b => b.Id == bookmarkId)
            .FirstOrDefaultAsync();
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

    public async Task<IEnumerable<AppUserPreferences>> GetAllPreferencesByFontAsync(string fontName)
    {
        return await _context.AppUserPreferences
            .Where(p => p.BookReaderFontFamily == fontName)
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
        var query = _context.AppUser.Includes(includeFlags);

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

        var libraryDtos = await _context.Library
            .Where(l => libraryIds.Contains(l.Id))
            .ProjectTo<LibraryDto>(_mapper.ConfigurationProvider)
            .ToListAsync();

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

    public async Task<AppUserSideNavStream?> GetSideNavStream(int streamId)
    {
        return await _context.AppUserSideNavStream
            .Include(d => d.SmartFilter)
            .FirstOrDefaultAsync(d => d.Id == streamId);
    }

    public async Task<AppUserSideNavStream?> GetSideNavStreamWithUser(int streamId)
    {
        return await _context.AppUserSideNavStream
            .Include(d => d.SmartFilter)
            .Include(d => d.AppUser)
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
    public async Task<AppUser?> GetUserByDeviceEmail(string deviceEmail)
    {
        return await _context.AppUser
            .Where(u => u.Devices.Any(d => d.EmailAddress == deviceEmail))
            .FirstOrDefaultAsync();
    }

    /// <summary>
    /// Returns a list of annotations ordered by page number.
    /// </summary>
    /// <param name="userId"></param>
    /// <param name="chapterId"></param>
    /// <returns></returns>
    public async Task<List<AnnotationDto>> GetAnnotations(int userId, int chapterId)
    {
        var userPreferences = await _context.AppUserPreferences.ToListAsync();

        return await _context.AppUserAnnotation
            .Where(a => a.ChapterId == chapterId)
            .RestrictBySocialPreferences(userId, userPreferences)
            .OrderBy(a => a.PageNumber)
            .ProjectTo<AnnotationDto>(_mapper.ConfigurationProvider)
            .ToListAsync();
    }

    public async Task<List<AnnotationDto>> GetAnnotationsByPage(int userId, int chapterId, int pageNum)
    {
        var userPreferences = await _context.AppUserPreferences.ToListAsync();

        return await _context.AppUserAnnotation
            .Where(a => a.ChapterId == chapterId && a.PageNumber == pageNum)
            .RestrictBySocialPreferences(userId, userPreferences)
            .OrderBy(a => a.PageNumber)
            .ProjectTo<AnnotationDto>(_mapper.ConfigurationProvider)
            .ToListAsync();
    }

    public async Task<AppUser?> GetByOidcId(string? oidcId, AppUserIncludes includes = AppUserIncludes.None)
    {
        if (string.IsNullOrEmpty(oidcId)) return null;

        return await _context.AppUser
            .Where(u => u.OidcId == oidcId)
            .Includes(includes)
            .FirstOrDefaultAsync();
    }

    public async Task<AnnotationDto?> GetAnnotationDtoById(int userId, int annotationId)
    {
        var userPreferences = await _context.AppUserPreferences.ToListAsync();

        return await _context.AppUserAnnotation
            .Where(a => a.Id == annotationId)
            .RestrictBySocialPreferences(userId, userPreferences)
            .ProjectTo<AnnotationDto>(_mapper.ConfigurationProvider)
            .FirstOrDefaultAsync();
    }

    public async Task<List<AnnotationDto>> GetAnnotationDtosBySeries(int userId, int seriesId)
    {
        var userPreferences = await _context.AppUserPreferences.ToListAsync();

        return await _context.AppUserAnnotation
            .Where(a => a.SeriesId == seriesId)
            .RestrictBySocialPreferences(userId, userPreferences)
            .ProjectTo<AnnotationDto>(_mapper.ConfigurationProvider)
            .ToListAsync();
    }

    public async Task UpdateUserAsActive(int userId)
    {
        await _context.Set<AppUser>()
            .Where(u => u.Id == userId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(u => u.LastActiveUtc, DateTime.UtcNow)
                .SetProperty(u => u.LastActive, DateTime.Now));
    }

    /// <summary>
    /// Retrieve all reviews (series and chapter) for a given user, respecting profile privacy settings and age restrictions.
    /// </summary>
    /// <param name="userId">UserId of source user</param>
    /// <param name="requestingUserId">Viewer UserId</param>
    /// <param name="query">Search text to match against Series name</param>
    /// <param name="ratingFilter">Rating, only applies to series/chapters rated. Will show everything greater or equal to</param>
    /// <returns></returns>
    public async Task<IList<UserReviewExtendedDto>> GetAllReviewsForUser(int userId, int requestingUserId, string? query = null, float? ratingFilter = null)
    {
        var bypassPreferences = userId == requestingUserId;
        if (!bypassPreferences)
        {
            var userPreferences = await _context.AppUserPreferences
                .FirstOrDefaultAsync(u => u.AppUserId == userId);

            if (userPreferences?.SocialPreferences?.ShareReviews == false)
            {
                return Array.Empty<UserReviewExtendedDto>();
            }
        }

        var userRating = await _context.AppUser.GetUserAgeRestriction(requestingUserId);

        // Get series-level reviews
        var seriesReviews = await _context.AppUserRating
            .WhereIf(ratingFilter is > 0, r => r.HasBeenRated && r.Rating >= ratingFilter!.Value)
            .Include(r => r.AppUser)
            .Include(r => r.Series)
            .ThenInclude(s => s.Metadata)
            .ThenInclude(sm => sm.People)
            .ThenInclude(smp => smp.Person)
            .Where(r => r.AppUserId == userId && !string.IsNullOrEmpty(r.Review))
            .WhereIf(!string.IsNullOrWhiteSpace(query), r => EF.Functions.Like(r.Series.Name, "%"+query+"%"))
            .RestrictAgainstAgeRestriction(userRating, requestingUserId)
            .OrderBy(r => r.SeriesId)
            .AsSplitQuery()
            .ProjectTo<UserReviewExtendedDto>(_mapper.ConfigurationProvider)
            .ToListAsync();

        // Get chapter-level reviews
        var chapterReviews = await _context.AppUserChapterRating
            .Include(r => r.AppUser)
            .Include(r => r.Series)
            .Include(r => r.Chapter)
            .ThenInclude(c => c.People)
            .Where(r => r.AppUserId == userId && !string.IsNullOrEmpty(r.Review))
            .RestrictAgainstAgeRestriction(userRating, requestingUserId)
            .OrderBy(r => r.SeriesId)
            .ThenBy(r => r.ChapterId)
            .AsSplitQuery()
            .ProjectTo<UserReviewExtendedDto>(_mapper.ConfigurationProvider)
            .ToListAsync();

        // Combine and return both lists
        return seriesReviews.Concat(chapterReviews).ToList();
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
        if (user == null) return ArraySegment<string>.Empty;

        // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
        if (_userManager == null)
        {
            // userManager is null on Unit Tests only
            return await _context.UserRoles
                .Where(ur => ur.UserId == userId)
                .Select(ur => ur.Role.Name)
                .ToListAsync();
        }

        return await _userManager.GetRolesAsync(user);
    }

    public async Task<IList<string>> GetRolesByAuthKey(string? apiKey)
    {
        if (string.IsNullOrEmpty(apiKey)) return ArraySegment<string>.Empty;

        var user = await _context.AppUserAuthKey
            .Where(k => k.Key == apiKey)
            .HasNotExpired()
            .Select(k => k.AppUser)
            .FirstOrDefaultAsync();
        if (user == null) return ArraySegment<string>.Empty;

        // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
        if (_userManager == null)
        {
            // userManager is null on Unit Tests only
            return await _context.UserRoles
                .Where(ur => ur.User.AuthKeys.Any(k => k.Key == apiKey && (k.ExpiresAtUtc == null || k.ExpiresAtUtc < DateTime.UtcNow)))
                .Select(ur => ur.Role.Name)
                .ToListAsync();
        }

        return await _userManager.GetRolesAsync(user);
    }

    public async Task<AppUserRating?> GetUserRatingAsync(int seriesId, int userId)
    {
        return await _context.AppUserRating
            .Where(r => r.SeriesId == seriesId && r.AppUserId == userId)
            .FirstOrDefaultAsync();
    }

    public async Task<AppUserChapterRating?> GetUserChapterRatingAsync(int userId, int chapterId)
    {
        return await _context.AppUserChapterRating
            .Where(r => r.AppUserId == userId && r.ChapterId == chapterId)
            .FirstOrDefaultAsync();
    }

    public async Task<IList<UserReviewDto>> GetUserRatingDtosForSeriesAsync(int seriesId, int userId)
    {
        var userPreferences = await _context.AppUserPreferences.ToListAsync();

        return await _context.AppUserRating
            .Include(r => r.AppUser)
            .Where(r => r.SeriesId == seriesId)
            .RestrictBySocialPreferences(userId, userPreferences)
            .OrderBy(r => r.AppUserId == userId)
            .ThenBy(r => r.Rating)
            .AsSplitQuery()
            .ProjectTo<UserReviewDto>(_mapper.ConfigurationProvider)
            .ToListAsync();
    }

    public async Task<IList<UserReviewDto>> GetUserRatingDtosForChapterAsync(int chapterId, int userId)
    {
        var userPreferences = await _context.AppUserPreferences.ToListAsync();

        return await _context.AppUserChapterRating
            .Include(r => r.AppUser)
            .Where(r => r.ChapterId == chapterId)
            .RestrictBySocialPreferences(userId, userPreferences)
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

    public async Task<UserDto?> GetUserDtoByAuthKeyAsync(string authKey)
    {
        if (string.IsNullOrEmpty(authKey)) return null;

        return await _context.AppUserAuthKey
            .Where(k => k.Key == authKey)
            .HasNotExpired()
            .Include(k => k.AppUser)
                .ThenInclude(u => u.UserRoles)
                    .ThenInclude(ur => ur.Role)
            .Select(k => k.AppUser)
            .ProjectTo<UserDto>(_mapper.ConfigurationProvider)
            .FirstOrDefaultAsync();
    }

    public async Task<int> GetUserIdByAuthKeyAsync(string authKey)
    {
        if (string.IsNullOrEmpty(authKey)) return 0;

        return await _context.AppUserAuthKey
            .Where(k => k.Key == authKey)
            .HasNotExpired()
            .Select(k => k.AppUserId)
            .FirstOrDefaultAsync();
    }

    public async Task<UserDto?> GetUserDtoById(int userId)
    {
        return await _context.AppUser
            .Where(u => u.Id == userId)
            .ProjectTo<UserDto>(_mapper.ConfigurationProvider)
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
                IdentityProvider = u.IdentityProvider,
                AgeRestriction = new AgeRestrictionDto()
                {
                    AgeRating = u.AgeRestriction,
                    IncludeUnknowns = u.AgeRestrictionIncludeUnknowns
                },
                Libraries =  u.Libraries.Select(l => new LibraryDto
                {
                    Id = l.Id,
                    Name = l.Name,
                    Type = l.Type,
                    LastScanned = l.LastScanned,
                    Folders = l.Folders.Select(x => x.Path).ToList()
                }).ToList(),
            })
            .AsSplitQuery()
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task<string?> GetCoverImageAsync(int userId, int requestingUserId)
    {
        // TODO: .NET JSON Support
        // return await _context.AppUser
        //     .Include(c => c.UserPreferences)
        //     .Where(c => c.Id == userId && c.UserPreferences.SocialPreferences.ShareProfile)
        //     .Select(c => c.CoverImage)
        //     .FirstOrDefaultAsync();

        var user = await _context.AppUser
            .Include(c => c.UserPreferences)
            .Where(c => c.Id == userId)
            .Select(c => new { c.CoverImage, c.UserPreferences })
            .FirstOrDefaultAsync();

        if (user?.UserPreferences?.SocialPreferences?.ShareProfile == true || userId == requestingUserId)
        {
            return user.CoverImage;
        }

        return null;
    }

    public async Task<string?> GetPersonCoverImageAsync(int personId)
    {
        return await _context.Person
            .Where(p => p.Id == personId)
            .Select(p => p.CoverImage)
            .FirstOrDefaultAsync();
    }

    public async Task<IList<AuthKeyDto>> GetAuthKeysForUserId(int userId)
    {
        return await _context.AppUserAuthKey
            .Where(k => k.AppUserId == userId)
            .ProjectTo<AuthKeyDto>(_mapper.ConfigurationProvider)
            .ToListAsync();
    }

    public async Task<IList<AuthKeyDto>> GetAllAuthKeysDtosWithExpiration()
    {
        return await _context.AppUserAuthKey
            .Where(k => k.ExpiresAtUtc != null)
            .ProjectTo<AuthKeyDto>(_mapper.ConfigurationProvider)
            .ToListAsync();
    }

    public async Task<AppUserAuthKey?> GetAuthKeyById(int authKeyId)
    {
        return await _context.AppUserAuthKey
            .Where(k => k.Id == authKeyId)
            .FirstOrDefaultAsync();
    }

    public async Task<DateTime?> GetAuthKeyExpiration(string authKey, int userId)
    {
        return await _context.AppUserAuthKey
            .Where(k => k.Key == authKey && k.AppUserId == userId)
            .Select(k => k.ExpiresAtUtc)
            .FirstOrDefaultAsync();
    }

    public async Task<AppUserSocialPreferences> GetSocialPreferencesForUser(int userId)
    {
        return await _context.AppUserPreferences
            .Where(p => p.AppUserId == userId)
            .Select(p => p.SocialPreferences)
            .FirstAsync();
    }

    public async Task<AppUserPreferences> GetPreferencesForUser(int userId)
    {
        return await _context.AppUserPreferences
            .Where(p => p.AppUserId == userId)
            .FirstAsync();
    }
}
