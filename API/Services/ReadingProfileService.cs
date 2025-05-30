using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using API.Data;
using API.Data.Repositories;
using API.DTOs;
using API.Entities;
using API.Extensions;
using API.Helpers.Builders;
using Kavita.Common;

namespace API.Services;

public interface IReadingProfileService
{
    /// <summary>
    /// Returns the ReadingProfile that should be applied to the given series, walks up the tree.
    /// Series (implicit) -> Series (Assigned) -> Library -> Default
    /// </summary>
    /// <param name="userId"></param>
    /// <param name="seriesId"></param>
    /// <returns></returns>
    Task<UserReadingProfileDto> GetReadingProfileForSeries(int userId, int seriesId);

    /// <summary>
    /// Updates a given reading profile for a user, and deletes all implicit profiles
    /// </summary>
    /// <param name="userId"></param>
    /// <param name="dto"></param>
    /// <returns></returns>
    /// <remarks>Does not update connected series and libraries</remarks>
    Task UpdateReadingProfile(int userId, UserReadingProfileDto dto);

    /// <summary>
    /// Creates a new reading profile for a user. Name must be unique per user
    /// </summary>
    /// <param name="userId"></param>
    /// <param name="dto"></param>
    /// <returns></returns>
    Task<UserReadingProfileDto> CreateReadingProfile(int userId, UserReadingProfileDto dto);

    /// <summary>
    /// Updates the implicit reading profile for a series, creates one if none exists
    /// </summary>
    /// <param name="userId"></param>
    /// <param name="seriesId"></param>
    /// <param name="dto"></param>
    /// <returns></returns>
    Task UpdateImplicitReadingProfile(int userId, int seriesId, UserReadingProfileDto dto);

    /// <summary>
    /// Deletes a given profile for a user
    /// </summary>
    /// <param name="userId"></param>
    /// <param name="profileId"></param>
    /// <returns></returns>
    /// <exception cref="UnauthorizedAccessException"></exception>
    /// <exception cref="KavitaException">The default profile for the user cannot be deleted</exception>
    Task DeleteReadingProfile(int userId, int profileId);

    /// <summary>
    /// Sets the given profile as global default
    /// </summary>
    /// <param name="userId"></param>
    /// <param name="profileId"></param>
    /// <returns></returns>
    Task SetDefaultReadingProfile(int userId, int profileId);

    Task AddProfileToSeries(int userId, int profileId, int seriesId);
    Task BulkAddProfileToSeries(int userId, int profileId, IList<int> seriesIds);
    Task ClearSeriesProfile(int userId, int seriesId);

    Task AddProfileToLibrary(int userId, int profileId, int libraryId);
    Task ClearLibraryProfile(int userId, int libraryId);

}

public class ReadingProfileService(IUnitOfWork unitOfWork, ILocalizationService localizationService): IReadingProfileService
{

    public async Task<UserReadingProfileDto> GetReadingProfileForSeries(int userId, int seriesId)
    {
        var seriesProfile = await unitOfWork.AppUserReadingProfileRepository.GetProfileDtoForSeries(userId, seriesId);
        if (seriesProfile != null) return seriesProfile;

        var series = await unitOfWork.SeriesRepository.GetSeriesByIdAsync(seriesId);
        if (series == null) throw new KavitaException(await localizationService.Translate(userId, "series-doesnt-exist"));

        var libraryProfile = await unitOfWork.AppUserReadingProfileRepository.GetProfileDtoForLibrary(userId, series.LibraryId);
        if (libraryProfile != null) return libraryProfile;

        var user = await unitOfWork.UserRepository.GetUserByIdAsync(userId, AppUserIncludes.UserPreferences);
        if (user == null) throw new UnauthorizedAccessException();

        return await unitOfWork.AppUserReadingProfileRepository.GetProfileDto(user.UserPreferences.DefaultReadingProfileId);
    }

    public async Task UpdateReadingProfile(int userId, UserReadingProfileDto dto)
    {
        var user = await unitOfWork.UserRepository.GetUserByIdAsync(userId, AppUserIncludes.UserPreferences);
        if (user == null) throw new UnauthorizedAccessException();

        var profile = await unitOfWork.AppUserReadingProfileRepository.GetProfile(dto.Id, ReadingProfileIncludes.Series);
        if (profile == null) throw new KavitaException("profile-does-not-exist");

        if (profile.UserId != userId) throw new UnauthorizedAccessException();

        UpdateReaderProfileFields(profile, dto);
        unitOfWork.AppUserReadingProfileRepository.Update(profile);

        // Remove all implicit profiles for series using this profile
        var allLinkedSeries = profile.Series.Select(sp => sp.SeriesId).ToList();
        var implicitProfiles = await unitOfWork.AppUserReadingProfileRepository.GetProfilesForSeries(userId, allLinkedSeries, true);
        unitOfWork.AppUserReadingProfileRepository.RemoveRange(implicitProfiles);

        await unitOfWork.CommitAsync();
    }

    public async Task<UserReadingProfileDto> CreateReadingProfile(int userId, UserReadingProfileDto dto)
    {
        var user = await unitOfWork.UserRepository.GetUserByIdAsync(userId, AppUserIncludes.UserPreferences);
        if (user == null) throw new UnauthorizedAccessException();

        var other = await unitOfWork.AppUserReadingProfileRepository.GetProfileByName(userId, dto.Name);
        if (other != null) throw new KavitaException("name-already-in-use");

        var newProfile = new AppUserReadingProfileBuilder(user.Id).Build();
        UpdateReaderProfileFields(newProfile, dto);
        unitOfWork.AppUserReadingProfileRepository.Add(newProfile);

        await unitOfWork.CommitAsync();

        return await unitOfWork.AppUserReadingProfileRepository.GetProfileDto(newProfile.Id);
    }

    public async Task UpdateImplicitReadingProfile(int userId, int seriesId, UserReadingProfileDto dto)
    {
        var user = await unitOfWork.UserRepository.GetUserByIdAsync(userId, AppUserIncludes.UserPreferences);
        if (user == null) throw new UnauthorizedAccessException();

        var existingProfile = await unitOfWork.AppUserReadingProfileRepository.GetProfileForSeries(userId, seriesId);

        // Series already had an implicit profile, update it
        if (existingProfile is {Implicit: true})
        {
            UpdateReaderProfileFields(existingProfile, dto, false);
            unitOfWork.AppUserReadingProfileRepository.Update(existingProfile);
            await unitOfWork.CommitAsync();
            return;
        }

        var series = await unitOfWork.SeriesRepository.GetSeriesByIdAsync(seriesId) ?? throw new KeyNotFoundException();
        var newProfile = new AppUserReadingProfileBuilder(userId)
            .WithSeries(series)
            .WithImplicit(true)
            .Build();

        // Set name to something fitting for debugging if needed
        UpdateReaderProfileFields(newProfile, dto, false);
        newProfile.Name = $"Implicit Profile for {seriesId}";
        newProfile.NormalizedName = newProfile.Name.ToNormalized();

        user.UserPreferences.ReadingProfiles.Add(newProfile);
        await unitOfWork.CommitAsync();
    }

    public async Task DeleteReadingProfile(int userId, int profileId)
    {
        var profile = await unitOfWork.AppUserReadingProfileRepository.GetProfile(profileId);
        if (profile == null) throw new KavitaException("profile-doesnt-exist");

        var user = await unitOfWork.UserRepository.GetUserByIdAsync(userId, AppUserIncludes.UserPreferences);
        if (user == null || profile.UserId != userId) throw new UnauthorizedAccessException();

        if (user.UserPreferences.DefaultReadingProfileId == profileId) throw new KavitaException("cant-delete-default-profile");

        unitOfWork.AppUserReadingProfileRepository.Remove(profile);
        await unitOfWork.CommitAsync();
    }

    public async Task SetDefaultReadingProfile(int userId, int profileId)
    {
        var profile = await unitOfWork.AppUserReadingProfileRepository.GetProfile(profileId);
        if (profile == null) throw new KavitaException("profile-not-found");

        if (profile.UserId != userId) throw new UnauthorizedAccessException();

        var user = await unitOfWork.UserRepository.GetUserByIdAsync(userId, AppUserIncludes.UserPreferences);
        if (user == null) throw new UnauthorizedAccessException();

        user.UserPreferences.DefaultReadingProfileId = profile.Id;
        await unitOfWork.CommitAsync();
    }

    public async Task AddProfileToSeries(int userId, int profileId, int seriesId)
    {
        var profile = await unitOfWork.AppUserReadingProfileRepository.GetProfile(profileId);
        if (profile == null) throw new KavitaException("profile-not-found");

        if (profile.UserId != userId) throw new UnauthorizedAccessException();

        // Remove all implicit profiles
        var implicitProfiles = await unitOfWork.AppUserReadingProfileRepository.GetProfilesForSeries(userId, [seriesId], true);
        unitOfWork.AppUserReadingProfileRepository.RemoveRange(implicitProfiles);

        var seriesProfile = await unitOfWork.AppUserReadingProfileRepository.GetSeriesProfile(userId, seriesId);
        if (seriesProfile != null)
        {
            seriesProfile.ReadingProfile = profile;
            await unitOfWork.CommitAsync();
            return;
        }

        seriesProfile = new SeriesReadingProfile
        {
            AppUserId = userId,
            SeriesId = seriesId,
            ReadingProfileId = profile.Id
        };

        unitOfWork.AppUserReadingProfileRepository.Add(seriesProfile);
        await unitOfWork.CommitAsync();
    }

    public async Task BulkAddProfileToSeries(int userId, int profileId, IList<int> seriesIds)
    {
        var profile = await unitOfWork.AppUserReadingProfileRepository.GetProfile(profileId, ReadingProfileIncludes.Series);
        if (profile == null) throw new KavitaException("profile-not-found");

        if (profile.UserId != userId) throw new UnauthorizedAccessException();

        var seriesProfiles = await unitOfWork.AppUserReadingProfileRepository.GetSeriesProfilesForSeries(userId, seriesIds);
        var newSeriesIds = seriesIds.Except(seriesProfiles.Select(p => p.SeriesId)).ToList();

        // Update existing
        foreach (var seriesProfile in seriesProfiles)
        {
            seriesProfile.ReadingProfile = profile;
        }

        // Create new ones
        foreach (var seriesId in newSeriesIds)
        {
            var seriesProfile = new SeriesReadingProfile
            {
                AppUserId = userId,
                SeriesId = seriesId,
                ReadingProfile = profile,
            };
            unitOfWork.AppUserReadingProfileRepository.Add(seriesProfile);
        }

        // Remove all implicit profiles
        var implicitProfiles = await unitOfWork.AppUserReadingProfileRepository.GetProfilesForSeries(userId, seriesIds, true);
        unitOfWork.AppUserReadingProfileRepository.RemoveRange(implicitProfiles);

        await unitOfWork.CommitAsync();
    }

    public async Task ClearSeriesProfile(int userId, int seriesId)
    {
        var profiles = await unitOfWork.AppUserReadingProfileRepository.GetAllProfilesForSeries(userId, seriesId, ReadingProfileIncludes.Series);
        if (!profiles.Any()) return;

        foreach (var profile in profiles)
        {
            if (profile.Implicit)
            {
                unitOfWork.AppUserReadingProfileRepository.Remove(profile);
            }
            else
            {
                profile.Series = profile.Series.Where(s => !(s.SeriesId == seriesId && s.AppUserId == userId)).ToList();
            }
        }

        await unitOfWork.CommitAsync();
    }

    public async Task AddProfileToLibrary(int userId, int profileId, int libraryId)
    {
        var profile = await unitOfWork.AppUserReadingProfileRepository.GetProfile(profileId);
        if (profile == null) throw new KavitaException("profile-not-found");

        if (profile.UserId != userId) throw new UnauthorizedAccessException();

        var libraryProfile = await unitOfWork.AppUserReadingProfileRepository.GetLibraryProfile(userId, libraryId);
        if (libraryProfile != null)
        {
            libraryProfile.ReadingProfile = profile;
            await unitOfWork.CommitAsync();
            return;
        }

        libraryProfile = new LibraryReadingProfile
        {
            AppUserId = userId,
            LibraryId = libraryId,
            ReadingProfile = profile,
        };

        unitOfWork.AppUserReadingProfileRepository.Add(libraryProfile);
        await unitOfWork.CommitAsync();
    }

    public async Task ClearLibraryProfile(int userId, int libraryId)
    {
        var profile = await unitOfWork.AppUserReadingProfileRepository.GetProfileForLibrary(userId, libraryId, ReadingProfileIncludes.Library);
        if (profile == null) return;

        if (profile.Implicit)
        {
            unitOfWork.AppUserReadingProfileRepository.Remove(profile);
            await unitOfWork.CommitAsync();
            return;
        }

        profile.Libraries = profile.Libraries
            .Where(s => !(s.LibraryId == libraryId && s.AppUserId == userId))
            .ToList();
        await unitOfWork.CommitAsync();
    }

    public static void UpdateReaderProfileFields(AppUserReadingProfile existingProfile, UserReadingProfileDto dto, bool updateName = true)
    {
        if (updateName && !string.IsNullOrEmpty(dto.Name) && existingProfile.NormalizedName != dto.Name.ToNormalized())
        {
            existingProfile.Name = dto.Name;
            existingProfile.NormalizedName = dto.Name.ToNormalized();
        }

        // Manga Reader
        existingProfile.ReadingDirection = dto.ReadingDirection;
        existingProfile.ScalingOption = dto.ScalingOption;
        existingProfile.PageSplitOption = dto.PageSplitOption;
        existingProfile.ReaderMode = dto.ReaderMode;
        existingProfile.AutoCloseMenu = dto.AutoCloseMenu;
        existingProfile.ShowScreenHints = dto.ShowScreenHints;
        existingProfile.EmulateBook = dto.EmulateBook;
        existingProfile.LayoutMode = dto.LayoutMode;
        existingProfile.BackgroundColor = string.IsNullOrEmpty(dto.BackgroundColor) ? "#000000" : dto.BackgroundColor;
        existingProfile.SwipeToPaginate = dto.SwipeToPaginate;
        existingProfile.AllowAutomaticWebtoonReaderDetection = dto.AllowAutomaticWebtoonReaderDetection;
        existingProfile.WidthOverride = dto.WidthOverride;

        // EpubReading
        existingProfile.BookReaderMargin = dto.BookReaderMargin;
        existingProfile.BookReaderLineSpacing = dto.BookReaderLineSpacing;
        existingProfile.BookReaderFontSize = dto.BookReaderFontSize;
        existingProfile.BookReaderFontFamily = dto.BookReaderFontFamily;
        existingProfile.BookReaderTapToPaginate = dto.BookReaderTapToPaginate;
        existingProfile.BookReaderReadingDirection = dto.BookReaderReadingDirection;
        existingProfile.BookReaderWritingStyle = dto.BookReaderWritingStyle;
        existingProfile.BookThemeName = dto.BookReaderThemeName;
        existingProfile.BookReaderLayoutMode = dto.BookReaderLayoutMode;
        existingProfile.BookReaderImmersiveMode = dto.BookReaderImmersiveMode;

        // Pdf Reading
        existingProfile.PdfTheme = dto.PdfTheme;
        existingProfile.PdfScrollMode = dto.PdfScrollMode;
        existingProfile.PdfSpreadMode = dto.PdfSpreadMode;
    }
}
