﻿using System.Collections.Generic;
using System.Threading.Tasks;
using API.Data.Repositories;
using API.DTOs;
using API.Entities;

namespace API.Interfaces.Repositories
{
    public interface IUserRepository
    {
        void Update(AppUser user);
        void Update(AppUserPreferences preferences);
        public void Delete(AppUser user);
        //Task<AppUser> GetUserByUsernameAsync(string username); // TODO: Validate all cases of this api
        Task<int> GetUserIdByUsernameAsync(string username);
        Task<AppUser> GetUserWithReadingListsByUsernameAsync(string username);
        Task<AppUser> GetUserByIdAsync(int id);
        Task<IEnumerable<MemberDto>>  GetMembersAsync();
        Task<IEnumerable<AppUser>> GetAdminUsersAsync();
        Task<AppUserRating> GetUserRating(int seriesId, int userId);
        void AddRatingTracking(AppUserRating userRating);
        Task<AppUserPreferences> GetPreferencesAsync(string username);
        Task<IEnumerable<BookmarkDto>> GetBookmarkDtosForSeries(int userId, int seriesId);
        Task<IEnumerable<BookmarkDto>> GetBookmarkDtosForVolume(int userId, int volumeId);
        Task<IEnumerable<BookmarkDto>> GetBookmarkDtosForChapter(int userId, int chapterId);
        Task<IEnumerable<BookmarkDto>> GetAllBookmarkDtos(int userId);
        Task<AppUser> GetUserByApiKeyAsync(string apiKey);
        Task<AppUser> GetUserByUsernameAsync(string username, AppUserIncludes includeFlags = AppUserIncludes.None);
        Task<AppUser> GetUserByIdAsync(int userId, AppUserIncludes includeFlags = AppUserIncludes.None);
    }
}
