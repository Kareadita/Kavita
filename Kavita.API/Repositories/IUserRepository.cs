using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Kavita.Models.DTOs;
using Kavita.Models.DTOs.Account;
using Kavita.Models.DTOs.Dashboard;
using Kavita.Models.DTOs.Filtering.v2;
using Kavita.Models.DTOs.KavitaPlus.Account;
using Kavita.Models.DTOs.Reader;
using Kavita.Models.DTOs.Scrobbling;
using Kavita.Models.DTOs.SeriesDetail;
using Kavita.Models.DTOs.SideNav;
using Kavita.Models.Entities.Enums.UserPreferences;
using Kavita.Models.Entities.User;

namespace Kavita.API.Repositories;

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
    #region Synchronous CRUD
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
    #endregion

    #region User Retrieval
    Task<IEnumerable<MemberDto>> GetEmailConfirmedMemberDtosAsync(bool emailConfirmed = true, CancellationToken ct = default);
    Task<IEnumerable<AppUser>> GetAdminUsersAsync(CancellationToken ct = default);
    Task<bool> IsUserAdminAsync(AppUser? user, CancellationToken ct = default);
    Task<IList<string>> GetRoles(int userId, CancellationToken ct = default);
    Task<IList<string>> GetRolesByAuthKey(string? apiKey, CancellationToken ct = default);
    Task<UserDto?> GetUserDtoByAuthKeyAsync(string authKey, CancellationToken ct = default);
    Task<int> GetUserIdByAuthKeyAsync(string authKey, CancellationToken ct = default);
    Task<UserDto?> GetUserDtoById(int userId, CancellationToken ct = default);
    Task<AppUser?> GetUserByUsernameAsync(string username, AppUserIncludes includeFlags = AppUserIncludes.None, CancellationToken ct = default);
    Task<AppUser?> GetUserByIdAsync(int userId, AppUserIncludes includeFlags = AppUserIncludes.None, CancellationToken ct = default);
    Task<AppUser?> GetUserByAuthKey(string authKey, AppUserIncludes includeFlags = AppUserIncludes.None, CancellationToken ct = default);
    Task<int> GetUserIdByUsernameAsync(string username, CancellationToken ct = default);
    Task<AppUser?> GetUserByEmailAsync(string email, AppUserIncludes includes = AppUserIncludes.None, CancellationToken ct = default);
    Task<IEnumerable<AppUser>> GetAllUsersAsync(AppUserIncludes includeFlags = AppUserIncludes.None, bool track = true, CancellationToken ct = default);
    Task<AppUser?> GetUserByConfirmationToken(string token, CancellationToken ct = default);
    Task<AppUser> GetDefaultAdminUser(AppUserIncludes includes = AppUserIncludes.None, CancellationToken ct = default);
    Task<IEnumerable<UserTokenInfo>> GetUserTokenInfo(CancellationToken ct = default);
    Task<AppUser?> GetUserByDeviceEmail(string deviceEmail, CancellationToken ct = default);
    Task<AppUser?> GetByOidcId(string? oidcId, AppUserIncludes includes = AppUserIncludes.None, CancellationToken ct = default);
    #endregion

    #region Ratings & Reviews
    Task<AppUserRating?> GetUserRatingAsync(int seriesId, int userId, CancellationToken ct = default);
    Task<AppUserChapterRating?> GetUserChapterRatingAsync(int userId, int chapterId, CancellationToken ct = default);
    Task<IList<UserReviewDto>> GetUserRatingDtosForSeriesAsync(int seriesId, int userId, CancellationToken ct = default);
    Task<IList<UserReviewDto>> GetUserRatingDtosForChapterAsync(int chapterId, int userId, CancellationToken ct = default);
    Task<IEnumerable<AppUserRating>> GetSeriesWithRatings(int userId, CancellationToken ct = default);
    Task<IEnumerable<AppUserRating>> GetSeriesWithReviews(int userId, CancellationToken ct = default);
    Task<IList<UserReviewExtendedDto>> GetAllReviewsForUser(int userId, int requestingUserId, string? query = null, float? ratingFilter = null, CancellationToken ct = default);
    #endregion

    #region Bookmarks
    Task<IEnumerable<BookmarkDto>> GetBookmarkDtosForSeries(int userId, int seriesId, CancellationToken ct = default);
    Task<IEnumerable<BookmarkDto>> GetBookmarkDtosForVolume(int userId, int volumeId, CancellationToken ct = default);
    Task<IEnumerable<BookmarkDto>> GetBookmarkDtosForChapter(int userId, int chapterId, CancellationToken ct = default);
    Task<IEnumerable<BookmarkDto>> GetAllBookmarkDtos(int userId, FilterV2Dto filter, CancellationToken ct = default);
    Task<IEnumerable<AppUserBookmark>> GetAllBookmarksAsync(CancellationToken ct = default);
    Task<AppUserBookmark?> GetBookmarkForPage(int page, int chapterId, int imageOffset, int userId, CancellationToken ct = default);
    Task<AppUserBookmark?> GetBookmarkAsync(int bookmarkId, CancellationToken ct = default);
    Task<IList<AppUserBookmark>> GetAllBookmarksByIds(IList<int> bookmarkIds, CancellationToken ct = default);
    #endregion

    #region Preferences & Settings
    Task<AppUserPreferences?> GetPreferencesAsync(string username, CancellationToken ct = default);
    Task<IEnumerable<AppUserPreferences>> GetAllPreferencesByThemeAsync(int themeId, CancellationToken ct = default);
    Task<IEnumerable<AppUserPreferences>> GetAllPreferencesByFontAsync(string fontName, CancellationToken ct = default);
    Task<string> GetLocale(int userId, CancellationToken ct = default);
    Task<AppUserSocialPreferences> GetSocialPreferencesForUser(int userId, CancellationToken ct = default);
    Task<AppUserPreferences> GetPreferencesForUser(int userId, CancellationToken ct = default);
    Task<AppUserOpdsPreferences> GetOpdsPreferences(int userId, CancellationToken ct = default);
    #endregion

    #region Permissions
    Task<bool> HasAccessToLibrary(int userId, int libraryId, CancellationToken ct = default);
    Task<bool> HasAccessToSeries(int userId, int seriesId, CancellationToken ct = default);
    Task<bool> HasAccessToVolume(int userId, int volumeId, CancellationToken ct = default);
    Task<bool> HasAccessToChapter(int userId, int chapterId, CancellationToken ct = default);
    Task<bool> HasAccessToPerson(int userId, int personId, CancellationToken ct = default);
    Task<bool> HasAccessToReadingList(int userId, int readingListId, CancellationToken ct = default);
    #endregion

    #region Scrobbling & Holds
    Task<bool> HasHoldOnSeries(int userId, int seriesId, CancellationToken ct = default);
    Task<IList<ScrobbleHoldDto>> GetHolds(int userId, CancellationToken ct = default);
    #endregion

    #region Streams (Dashboard & SideNav)
    Task<IList<DashboardStreamDto>> GetDashboardStreams(int userId, bool visibleOnly = false, CancellationToken ct = default);
    Task<IList<AppUserDashboardStream>> GetAllDashboardStreams(CancellationToken ct = default);
    Task<AppUserDashboardStream?> GetDashboardStream(int streamId, CancellationToken ct = default);
    Task<IList<AppUserDashboardStream>> GetDashboardStreamWithFilter(int filterId, CancellationToken ct = default);
    Task<IList<SideNavStreamDto>> GetSideNavStreams(int userId, bool visibleOnly = false, CancellationToken ct = default);
    Task<AppUserSideNavStream?> GetSideNavStream(int streamId, CancellationToken ct = default);
    Task<AppUserSideNavStream?> GetSideNavStreamWithUser(int streamId, CancellationToken ct = default);
    Task<IList<AppUserSideNavStream>> GetSideNavStreamWithFilter(int filterId, CancellationToken ct = default);
    Task<IList<AppUserSideNavStream>> GetSideNavStreamsByLibraryId(int libraryId, CancellationToken ct = default);
    Task<IList<AppUserSideNavStream>> GetSideNavStreamWithExternalSource(int externalSourceId, CancellationToken ct = default);
    Task<IList<AppUserSideNavStream>> GetDashboardStreamsByIds(IList<int> streamIds, CancellationToken ct = default);
    #endregion

    #region Annotations
    Task<List<AnnotationDto>> GetAnnotations(int userId, int chapterId, CancellationToken ct = default);
    Task<List<AnnotationDto>> GetAnnotationsByPage(int userId, int chapterId, int pageNum, CancellationToken ct = default);
    Task<AnnotationDto?> GetAnnotationDtoById(int userId, int annotationId, CancellationToken ct = default);
    Task<List<AnnotationDto>> GetAnnotationDtosBySeries(int userId, int seriesId, CancellationToken ct = default);
    #endregion

    #region Images & Media
    Task<string?> GetCoverImageAsync(int userId, CancellationToken ct = default);
    Task<string?> GetPersonCoverImageAsync(int personId, CancellationToken ct = default);
    #endregion

    #region Auth Keys
    Task<IList<AuthKeyDto>> GetAuthKeysForUserId(int userId, CancellationToken ct = default);
    Task<IList<AuthKeyDto>> GetAllAuthKeysDtosWithExpiration(CancellationToken ct = default);
    Task<AppUserAuthKey?> GetAuthKeyById(int authKeyId, CancellationToken ct = default);
    Task<DateTime?> GetAuthKeyExpiration(string authKey, int userId, CancellationToken ct = default);
    #endregion
}
