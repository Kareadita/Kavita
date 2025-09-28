using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using API.Data;
using API.Data.Repositories;
using API.DTOs;
using API.Entities;
using API.Entities.Enums;
using API.Extensions;
using API.Helpers.Builders;
using AutoMapper;
using Kavita.Common;

namespace API.Services;
#nullable enable

public interface IReadingProfileService
{
    /// <summary>
    /// Returns the ReadingProfile that should be applied to the given series, walks up the tree.
    /// Series (Implicit) -> Series (User) -> Library (User) -> Default
    /// </summary>
    /// <param name="userId"></param>
    /// <param name="seriesId"></param>
    /// <param name="skipImplicit"></param>
    /// <returns></returns>
    Task<UserReadingProfileDto> GetReadingProfileDtoForSeries(int userId, int seriesId, bool skipImplicit = false);

    /// <summary>
    /// Creates a new reading profile for a user. Name must be unique per user
    /// </summary>
    /// <param name="userId"></param>
    /// <param name="dto"></param>
    /// <returns></returns>
    Task<UserReadingProfileDto> CreateReadingProfile(int userId, UserReadingProfileDto dto);
    Task<UserReadingProfileDto> PromoteImplicitProfile(int userId, int profileId);

    /// <summary>
    /// Updates the implicit reading profile for a series, creates one if none exists
    /// </summary>
    /// <param name="userId"></param>
    /// <param name="seriesId"></param>
    /// <param name="dto"></param>
    /// <returns></returns>
    Task<UserReadingProfileDto> UpdateImplicitReadingProfile(int userId, int seriesId, UserReadingProfileDto dto);

    /// <summary>
    /// Updates the non-implicit reading profile for the given series, and removes implicit profiles
    /// </summary>
    /// <param name="userId"></param>
    /// <param name="seriesId"></param>
    /// <param name="dto"></param>
    /// <returns></returns>
    Task<UserReadingProfileDto> UpdateParent(int userId, int seriesId, UserReadingProfileDto dto);

    /// <summary>
    /// Updates a given reading profile for a user
    /// </summary>
    /// <param name="userId"></param>
    /// <param name="dto"></param>
    /// <returns></returns>
    /// <remarks>Does not update connected series and libraries</remarks>
    Task<UserReadingProfileDto> UpdateReadingProfile(int userId, UserReadingProfileDto dto);

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
    /// Binds the reading profile to the series, and remove the implicit RP from the series if it exists
    /// </summary>
    /// <param name="userId"></param>
    /// <param name="profileId"></param>
    /// <param name="seriesId"></param>
    /// <returns></returns>
    Task AddProfileToSeries(int userId, int profileId, int seriesId);
    /// <summary>
    /// Binds the reading profile to many series, and remove the implicit RP from the series if it exists
    /// </summary>
    /// <param name="userId"></param>
    /// <param name="profileId"></param>
    /// <param name="seriesIds"></param>
    /// <returns></returns>
    Task BulkAddProfileToSeries(int userId, int profileId, IList<int> seriesIds);
    /// <summary>
    /// Remove all reading profiles bound to the series
    /// </summary>
    /// <param name="userId"></param>
    /// <param name="seriesId"></param>
    /// <returns></returns>
    Task ClearSeriesProfile(int userId, int seriesId);

    /// <summary>
    /// Bind the reading profile to the library
    /// </summary>
    /// <param name="userId"></param>
    /// <param name="profileId"></param>
    /// <param name="libraryId"></param>
    /// <returns></returns>
    Task AddProfileToLibrary(int userId, int profileId, int libraryId);
    /// <summary>
    /// Remove the reading profile bound to the library, if it exists
    /// </summary>
    /// <param name="userId"></param>
    /// <param name="libraryId"></param>
    /// <returns></returns>
    Task ClearLibraryProfile(int userId, int libraryId);
    /// <summary>
    /// Returns the bound Reading Profile to a Library
    /// </summary>
    /// <param name="userId"></param>
    /// <param name="libraryId"></param>
    /// <returns></returns>
    Task<UserReadingProfileDto?> GetReadingProfileDtoForLibrary(int userId, int libraryId);
}

public class ReadingProfileService(IUnitOfWork unitOfWork, ILocalizationService localizationService, IMapper mapper): IReadingProfileService
{
    /// <summary>
    /// Tries to resolve the Reading Profile for a given Series. Will first check (optionally) Implicit profiles, then check for a bound Series profile, then a bound
    /// Library profile, then default to the default profile.
    /// </summary>
    /// <param name="userId"></param>
    /// <param name="seriesId"></param>
    /// <param name="skipImplicit"></param>
    /// <returns></returns>
    /// <exception cref="KavitaException"></exception>
    public async Task<AppUserReadingProfile> GetReadingProfileForSeries(int userId, int seriesId, bool skipImplicit = false)
    {
        var profiles = await unitOfWork.AppUserReadingProfileRepository.GetProfilesForUser(userId, skipImplicit);

        // If there is an implicit, send back
        var implicitProfile =
            profiles.FirstOrDefault(p => p.SeriesIds.Contains(seriesId) && p.Kind == ReadingProfileKind.Implicit);
        if (implicitProfile != null) return  implicitProfile;

        // Next check for a bound Series profile
        var seriesProfile = profiles
            .FirstOrDefault(p => p.SeriesIds.Contains(seriesId) && p.Kind != ReadingProfileKind.Implicit);
        if (seriesProfile != null) return seriesProfile;

        // Check for a library bound profile
        var series = await unitOfWork.SeriesRepository.GetSeriesByIdAsync(seriesId);
        if (series == null) throw new KavitaException(await localizationService.Translate(userId, "series-doesnt-exist"));

        var libraryProfile = profiles
            .FirstOrDefault(p => p.LibraryIds.Contains(series.LibraryId) && p.Kind != ReadingProfileKind.Implicit);
        if (libraryProfile != null) return libraryProfile;

        // Fallback to the default profile
        return profiles.First(p => p.Kind == ReadingProfileKind.Default);
    }

    public async Task<UserReadingProfileDto> GetReadingProfileDtoForSeries(int userId, int seriesId, bool skipImplicit = false)
    {
        return mapper.Map<UserReadingProfileDto>(await GetReadingProfileForSeries(userId, seriesId, skipImplicit));
    }

    public async Task<UserReadingProfileDto> UpdateParent(int userId, int seriesId, UserReadingProfileDto dto)
    {
        var parentProfile = await GetReadingProfileForSeries(userId, seriesId, true);

        UpdateReaderProfileFields(parentProfile, dto, false);
        unitOfWork.AppUserReadingProfileRepository.Update(parentProfile);

        // Remove the implicit profile when we UpdateParent (from reader) as it is implied that we are already bound with a non-implicit profile
        await DeleteImplicateReadingProfilesForSeries(userId, [seriesId]);

        await unitOfWork.CommitAsync();
        return mapper.Map<UserReadingProfileDto>(parentProfile);
    }

    public async Task<UserReadingProfileDto> UpdateReadingProfile(int userId, UserReadingProfileDto dto)
    {
        var profile = await unitOfWork.AppUserReadingProfileRepository.GetUserProfile(userId, dto.Id);
        if (profile == null) throw new KavitaException("profile-does-not-exist");

        UpdateReaderProfileFields(profile, dto);
        unitOfWork.AppUserReadingProfileRepository.Update(profile);

        await unitOfWork.CommitAsync();
        return mapper.Map<UserReadingProfileDto>(profile);
    }

    public async Task<UserReadingProfileDto> CreateReadingProfile(int userId, UserReadingProfileDto dto)
    {
        var user = await unitOfWork.UserRepository.GetUserByIdAsync(userId, AppUserIncludes.UserPreferences);
        if (user == null) throw new UnauthorizedAccessException();

        if (await unitOfWork.AppUserReadingProfileRepository.IsProfileNameInUse(userId, dto.Name)) throw new KavitaException("name-already-in-use");

        var newProfile = new AppUserReadingProfileBuilder(user.Id).Build();
        UpdateReaderProfileFields(newProfile, dto);

        unitOfWork.AppUserReadingProfileRepository.Add(newProfile);
        user.ReadingProfiles.Add(newProfile);

        await unitOfWork.CommitAsync();

        return mapper.Map<UserReadingProfileDto>(newProfile);
    }

    /// <summary>
    /// Promotes the implicit profile to a user profile. Removes the series from other profiles.
    /// </summary>
    /// <param name="userId"></param>
    /// <param name="profileId"></param>
    /// <returns></returns>
    public async Task<UserReadingProfileDto> PromoteImplicitProfile(int userId, int profileId)
    {
        // Get all the user's profiles including the implicit
        var allUserProfiles = await unitOfWork.AppUserReadingProfileRepository.GetProfilesForUser(userId, false);
        var profileToPromote = allUserProfiles.First(r => r.Id == profileId);
        var seriesId = profileToPromote.SeriesIds[0]; // An Implicit series can only be bound to 1 Series

        // Check if there are any reading profiles (Series) already bound to the series
        var existingSeriesProfile = allUserProfiles.FirstOrDefault(r => r.SeriesIds.Contains(seriesId) && r.Kind == ReadingProfileKind.User);
        if (existingSeriesProfile != null)
        {
            existingSeriesProfile.SeriesIds.Remove(seriesId);
            unitOfWork.AppUserReadingProfileRepository.Update(existingSeriesProfile);
        }

        // Convert the implicit profile into a proper Series
        var series = await unitOfWork.SeriesRepository.GetSeriesByIdAsync(seriesId);
        if (series == null) throw new KavitaException("series-doesnt-exist"); // Shouldn't happen

        profileToPromote.Kind = ReadingProfileKind.User;
        profileToPromote.Name = await localizationService.Translate(userId, "generated-reading-profile-name", series.Name);
        profileToPromote.Name = EnsureUniqueProfileName(allUserProfiles, profileToPromote.Name);
        profileToPromote.NormalizedName = profileToPromote.Name.ToNormalized();
        unitOfWork.AppUserReadingProfileRepository.Update(profileToPromote);

        await unitOfWork.CommitAsync();

        return mapper.Map<UserReadingProfileDto>(profileToPromote);
    }

    private static string EnsureUniqueProfileName(IList<AppUserReadingProfile> allUserProfiles, string name)
    {
        var counter = 1;
        var newName = name;
        while (allUserProfiles.Any(p => p.Name == newName))
        {
            newName = $"{name} ({counter})";
            counter++;
        }

        return newName;
    }

    public async Task<UserReadingProfileDto> UpdateImplicitReadingProfile(int userId, int seriesId, UserReadingProfileDto dto)
    {
        var user = await unitOfWork.UserRepository.GetUserByIdAsync(userId, AppUserIncludes.UserPreferences);
        if (user == null) throw new UnauthorizedAccessException();

        var profiles =  await unitOfWork.AppUserReadingProfileRepository.GetProfilesForUser(userId);
        var existingProfile = profiles.FirstOrDefault(rp => rp.Kind == ReadingProfileKind.Implicit && rp.SeriesIds.Contains(seriesId));

        // Series already had an implicit profile, update it
        if (existingProfile is {Kind: ReadingProfileKind.Implicit})
        {
            UpdateReaderProfileFields(existingProfile, dto, false);
            unitOfWork.AppUserReadingProfileRepository.Update(existingProfile);
            await unitOfWork.CommitAsync();

            return mapper.Map<UserReadingProfileDto>(existingProfile);
        }

        var series = await unitOfWork.SeriesRepository.GetSeriesByIdAsync(seriesId) ?? throw new KeyNotFoundException();
        var newProfile = new AppUserReadingProfileBuilder(userId)
            .WithSeries(series)
            .WithKind(ReadingProfileKind.Implicit)
            .Build();

        // Set name to something fitting for debugging if needed
        UpdateReaderProfileFields(newProfile, dto, false);
        newProfile.Name = $"Implicit Profile for {seriesId}";
        newProfile.NormalizedName = newProfile.Name.ToNormalized();

        user.ReadingProfiles.Add(newProfile);
        await unitOfWork.CommitAsync();

        return mapper.Map<UserReadingProfileDto>(newProfile);
    }

    public async Task DeleteReadingProfile(int userId, int profileId)
    {
        var profile = await unitOfWork.AppUserReadingProfileRepository.GetUserProfile(userId, profileId);
        if (profile == null) throw new KavitaException("profile-doesnt-exist");

        if (profile.Kind == ReadingProfileKind.Default) throw new KavitaException("cant-delete-default-profile");

        unitOfWork.AppUserReadingProfileRepository.Remove(profile);
        await unitOfWork.CommitAsync();
    }

    public async Task AddProfileToSeries(int userId, int profileId, int seriesId)
    {
        var profile = await unitOfWork.AppUserReadingProfileRepository.GetUserProfile(userId, profileId);
        if (profile == null) throw new KavitaException("profile-doesnt-exist");

        await DeleteImplicitAndRemoveFromUserProfiles(userId, [seriesId], []);

        profile.SeriesIds.Add(seriesId);
        unitOfWork.AppUserReadingProfileRepository.Update(profile);

        await unitOfWork.CommitAsync();
    }

    public async Task BulkAddProfileToSeries(int userId, int profileId, IList<int> seriesIds)
    {
        var profile = await unitOfWork.AppUserReadingProfileRepository.GetUserProfile(userId, profileId);
        if (profile == null) throw new KavitaException("profile-doesnt-exist");

        await DeleteImplicitAndRemoveFromUserProfiles(userId, seriesIds, []);

        profile.SeriesIds.AddRange(seriesIds.Except(profile.SeriesIds));
        unitOfWork.AppUserReadingProfileRepository.Update(profile);

        await unitOfWork.CommitAsync();
    }

    public async Task ClearSeriesProfile(int userId, int seriesId)
    {
        await DeleteImplicitAndRemoveFromUserProfiles(userId, [seriesId], []);
        await unitOfWork.CommitAsync();
    }

    public async Task AddProfileToLibrary(int userId, int profileId, int libraryId)
    {
        var profile = await unitOfWork.AppUserReadingProfileRepository.GetUserProfile(userId, profileId);
        if (profile == null) throw new KavitaException("profile-doesnt-exist");

        await DeleteImplicitAndRemoveFromUserProfiles(userId, [], [libraryId]);

        profile.LibraryIds.Add(libraryId);
        unitOfWork.AppUserReadingProfileRepository.Update(profile);
        await unitOfWork.CommitAsync();
    }

    public async Task ClearLibraryProfile(int userId, int libraryId)
    {
        var profiles =  await unitOfWork.AppUserReadingProfileRepository.GetProfilesForUser(userId);
        var libraryProfile = profiles.FirstOrDefault(p => p.LibraryIds.Contains(libraryId));
        if (libraryProfile != null)
        {
            libraryProfile.LibraryIds.Remove(libraryId);
            unitOfWork.AppUserReadingProfileRepository.Update(libraryProfile);
        }


        if (unitOfWork.HasChanges())
        {
            await unitOfWork.CommitAsync();
        }
    }

    public async Task<UserReadingProfileDto?> GetReadingProfileDtoForLibrary(int userId, int libraryId)
    {
        var profiles = await unitOfWork.AppUserReadingProfileRepository.GetProfilesForUser(userId, true);
        return mapper.Map<UserReadingProfileDto>(profiles.FirstOrDefault(p => p.LibraryIds.Contains(libraryId)));
    }

    private async Task DeleteImplicitAndRemoveFromUserProfiles(int userId, IList<int> seriesIds, IList<int> libraryIds)
    {
        var profiles =  await unitOfWork.AppUserReadingProfileRepository.GetProfilesForUser(userId);
        var implicitProfiles = profiles
            .Where(rp => rp.SeriesIds.Intersect(seriesIds).Any())
            .Where(rp => rp.Kind == ReadingProfileKind.Implicit)
            .ToList();
        unitOfWork.AppUserReadingProfileRepository.RemoveRange(implicitProfiles);

        var nonImplicitProfiles = profiles
            .Where(rp => rp.SeriesIds.Intersect(seriesIds).Any() || rp.LibraryIds.Intersect(libraryIds).Any())
            .Where(rp => rp.Kind != ReadingProfileKind.Implicit);

        foreach (var profile in nonImplicitProfiles)
        {
            profile.SeriesIds.RemoveAll(seriesIds.Contains);
            profile.LibraryIds.RemoveAll(libraryIds.Contains);
            unitOfWork.AppUserReadingProfileRepository.Update(profile);
        }
    }

    private async Task DeleteImplicateReadingProfilesForSeries(int userId, IList<int> seriesIds)
    {
        var profiles =  await unitOfWork.AppUserReadingProfileRepository.GetProfilesForUser(userId);
        var implicitProfiles = profiles
            .Where(rp => rp.SeriesIds.Intersect(seriesIds).Any())
            .Where(rp => rp.Kind == ReadingProfileKind.Implicit)
            .ToList();
        unitOfWork.AppUserReadingProfileRepository.RemoveRange(implicitProfiles);
    }

    private async Task RemoveSeriesFromUserProfiles(int userId, IList<int> seriesIds)
    {
        var profiles =  await unitOfWork.AppUserReadingProfileRepository.GetProfilesForUser(userId);
        var userProfiles = profiles
            .Where(rp => rp.SeriesIds.Intersect(seriesIds).Any())
            .Where(rp => rp.Kind == ReadingProfileKind.User)
            .ToList();

        unitOfWork.AppUserReadingProfileRepository.RemoveRange(userProfiles);
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
        existingProfile.DisableWidthOverride = dto.DisableWidthOverride;

        // Book Reader
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

        // PDF Reading
        existingProfile.PdfTheme = dto.PdfTheme;
        existingProfile.PdfScrollMode = dto.PdfScrollMode;
        existingProfile.PdfSpreadMode = dto.PdfSpreadMode;
    }
}
