using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using Kavita.API.Database;
using Kavita.API.Repositories;
using Kavita.API.Services;
using Kavita.API.Services.Reading;
using Kavita.Common;
using Kavita.Common.Extensions;
using Kavita.Models.Builders;
using Kavita.Models.DTOs;
using Kavita.Models.Entities.Enums;
using Kavita.Models.Entities.User;
using Microsoft.EntityFrameworkCore;

namespace Kavita.Services.Reading;

public class ReadingProfileService(IUnitOfWork unitOfWork, ILocalizationService localizationService, IMapper mapper): IReadingProfileService
{
    public async Task<AppUserReadingProfile> GetReadingProfileForSeries(int userId, int libraryId, int seriesId,
        int? activeDeviceId, bool skipImplicit = false)
    {
        return await unitOfWork.AppUserReadingProfileRepository.GetProfileForSeries(userId, libraryId, seriesId,
            activeDeviceId, skipImplicit);
    }

    public async Task<UserReadingProfileDto> GetReadingProfileDtoForSeries(int userId, int libraryId, int seriesId,
        int? activeDeviceId, bool skipImplicit = false)
    {
        return mapper.Map<UserReadingProfileDto>(await GetReadingProfileForSeries(userId, libraryId, seriesId,
            activeDeviceId, skipImplicit));
    }

    public async Task<UserReadingProfileDto> UpdateParent(int userId, int libraryId, int seriesId,
        UserReadingProfileDto dto, int? activeDeviceId)
    {
        var profile = await unitOfWork.AppUserReadingProfileRepository.GetUserProfile(userId, dto.Id);
        if (profile == null) throw new KavitaException("profile-does-not-exist");

        var parentProfile = await GetReadingProfileForSeries(userId, libraryId, seriesId, activeDeviceId, true);

        UpdateReaderProfileFields(parentProfile, dto, false);
        unitOfWork.AppUserReadingProfileRepository.Update(parentProfile);

        // Delete profile as we'll be using the parent now
        unitOfWork.AppUserReadingProfileRepository.Remove(profile);

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

    public async Task<UserReadingProfileDto> PromoteImplicitProfile(int userId, int profileId, int? activeDeviceId)
    {
        // Get all the user's profiles including the implicit
        var allUserProfiles = await unitOfWork.AppUserReadingProfileRepository.GetProfilesForUser(userId);
        var profileToPromote = allUserProfiles.FirstOrDefault(rp => rp.Id == profileId);

        if (profileToPromote == null) throw new KavitaException("profile-does-not-exist");
        if (profileToPromote.Kind != ReadingProfileKind.Implicit) throw new KavitaException("profile-not-implicit");

        var seriesId = profileToPromote.SeriesIds[0]; // An Implicit series can only be bound to 1 Series

        // Check if there are any reading profiles (Series) already bound to the series (and device)
        var existingSeriesProfile = allUserProfiles
            .Where(rp => rp.Kind == ReadingProfileKind.User)
            .Where(rp => activeDeviceId == null || rp.DeviceIds.Contains(activeDeviceId.Value))
            .FirstOrDefault(rp => rp.SeriesIds.Contains(seriesId));

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

    public async Task<UserReadingProfileDto> UpdateImplicitReadingProfile(int userId, int libraryId, int seriesId,
        UserReadingProfileDto dto, int? activeDeviceId)
    {
        var user = await unitOfWork.UserRepository.GetUserByIdAsync(userId, AppUserIncludes.UserPreferences);
        if (user == null) throw new UnauthorizedAccessException();

        var existingProfile = await unitOfWork.AppUserReadingProfileRepository
            .GetProfileForSeries(userId, libraryId, seriesId, activeDeviceId);

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
        if (activeDeviceId != null)
        {
            newProfile.DeviceIds.Add(activeDeviceId.Value);
        }

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

    public async Task SetSeriesProfiles(int userId, List<int> profileIds, int seriesId)
    {
        var profiles = await unitOfWork.AppUserReadingProfileRepository.GetProfilesForUser(userId);

        var selectedProfiles = profiles
            .Where(rp => profileIds.Contains(rp.Id))
            .ToList();
        if (selectedProfiles.Count != profileIds.Count) throw new KavitaException("profile-doesnt-exist");

        DeviceOverlapGuard(selectedProfiles);

        var allDeviceIds = selectedProfiles.SelectMany(p => p.DeviceIds).Distinct().ToList();

        DeleteImplicitAndRemoveFromUserProfiles(profiles, [seriesId], [], allDeviceIds);

        foreach (var profile in selectedProfiles)
        {
            profile.SeriesIds.Add(seriesId);
            unitOfWork.AppUserReadingProfileRepository.Update(profile);
        }

        await unitOfWork.CommitAsync();
    }

    public async Task BulkSetSeriesProfiles(int userId, List<int> profileIds, List<int> seriesIds)
    {
        var profiles = await unitOfWork.AppUserReadingProfileRepository.GetProfilesForUser(userId);

        var selectedProfiles = profiles
            .Where(rp => profileIds.Contains(rp.Id))
            .ToList();
        if (selectedProfiles.Count != profileIds.Count) throw new KavitaException("profile-doesnt-exist");

        DeviceOverlapGuard(selectedProfiles);

        var allDeviceIds = selectedProfiles.SelectMany(p => p.DeviceIds).Distinct().ToList();

        DeleteImplicitAndRemoveFromUserProfiles(profiles, seriesIds, [], allDeviceIds);

        foreach (var profile in selectedProfiles)
        {
            profile.SeriesIds.AddRange(seriesIds.Except(profile.SeriesIds));
            unitOfWork.AppUserReadingProfileRepository.Update(profile);
        }

        await unitOfWork.CommitAsync();
    }

    public async Task ClearSeriesProfile(int userId, int seriesId)
    {
        // Null device ids, delete all
        var profiles = await unitOfWork.AppUserReadingProfileRepository.GetProfilesForUser(userId);
        DeleteImplicitAndRemoveFromUserProfiles(profiles, [seriesId], [], null);
        await unitOfWork.CommitAsync();
    }

    public async Task SetLibraryProfiles(int userId, List<int> profileIds, int libraryId)
    {
        var profiles = await unitOfWork.AppUserReadingProfileRepository.GetProfilesForUser(userId);

        var selectedProfiles = profiles
            .Where(rp => profileIds.Contains(rp.Id))
            .ToList();
        if (selectedProfiles.Count != profileIds.Count) throw new KavitaException("profile-doesnt-exist");

        DeviceOverlapGuard(selectedProfiles);

        var allDeviceIds = selectedProfiles.SelectMany(p => p.DeviceIds).Distinct().ToList();

        DeleteImplicitAndRemoveFromUserProfiles(profiles, [], [libraryId], allDeviceIds);

        foreach (var profile in selectedProfiles)
        {
            profile.LibraryIds.Add(libraryId);
            unitOfWork.AppUserReadingProfileRepository.Update(profile);
        }

        await unitOfWork.CommitAsync();
    }

    public async Task ClearLibraryProfile(int userId, int libraryId)
    {
        var profiles = await unitOfWork.AppUserReadingProfileRepository.GetProfilesForLibrary(userId, libraryId);

        foreach (var profile in profiles)
        {
            profile.LibraryIds.Remove(libraryId);
            unitOfWork.AppUserReadingProfileRepository.Update(profile);
        }

        if (unitOfWork.HasChanges())
        {
            await unitOfWork.CommitAsync();
        }
    }

    public Task<List<UserReadingProfileDto>> GetReadingProfileDtosForLibrary(int userId, int libraryId)
    {
        return unitOfWork.DataContext.AppUserReadingProfiles
            .Where(rp => rp.AppUserId == userId && rp.LibraryIds.Contains(libraryId))
            .Where(rp => rp.Kind == ReadingProfileKind.User)
            .ProjectTo<UserReadingProfileDto>(mapper.ConfigurationProvider)
            .ToListAsync();
    }

    public Task<List<UserReadingProfileDto>> GetReadingProfileDtosForSeries(int userId, int seriesId)
    {
        return unitOfWork.DataContext.AppUserReadingProfiles
            .Where(rp => rp.AppUserId == userId && rp.SeriesIds.Contains(seriesId))
            .Where(rp => rp.Kind == ReadingProfileKind.User)
            .ProjectTo<UserReadingProfileDto>(mapper.ConfigurationProvider)
            .ToListAsync();
    }

    public async Task SetProfileDevices(int userId, int profileId, List<int> deviceIds)
    {
        var profile = await unitOfWork.AppUserReadingProfileRepository.GetUserProfile(userId, profileId);
        if (profile == null) throw new KavitaException("profile-doesnt-exist");

        if (profile.Kind == ReadingProfileKind.Default) throw new KavitaException("cant-assign-devices-to-default");

        profile.DeviceIds = deviceIds;
        unitOfWork.AppUserReadingProfileRepository.Update(profile);

        await unitOfWork.CommitAsync();

        // Remove series & library links from profiles where there is now overlap with devices
        // E.g. for the same series there are now two profiles that would match
        var profiles = await unitOfWork.AppUserReadingProfileRepository.GetProfilesForUser(userId);

        var overlappingProfiles = profiles
            .Where(rp => rp.Id != profileId)
            .Where(rp => rp.Kind == ReadingProfileKind.User)
            .Where(rp => (rp.DeviceIds.Count == 0 && deviceIds.Count == 0)
                         || rp.DeviceIds.Intersect(deviceIds).Any())
            .Where(rp => rp.SeriesIds.Intersect(profile.SeriesIds).Any()
                         || rp.LibraryIds.Intersect(profile.LibraryIds).Any());

        foreach (var overlap in overlappingProfiles)
        {
            overlap.SeriesIds.RemoveAll(profile.SeriesIds.Contains);
            overlap.LibraryIds.RemoveAll(profile.LibraryIds.Contains);

            unitOfWork.AppUserReadingProfileRepository.Update(overlap);
        }

        await unitOfWork.CommitAsync();
    }

    public async Task RemoveDeviceLinks(int userId, int deviceId)
    {
        var profiles = await unitOfWork.DataContext.AppUserReadingProfiles
            .Where(rp => rp.AppUserId == userId && rp.DeviceIds.Contains(deviceId))
            .ToListAsync();

        foreach (var profile in profiles)
        {
            profile.DeviceIds.Remove(deviceId);
            unitOfWork.AppUserReadingProfileRepository.Update(profile);
        }
    }

    private static void DeviceOverlapGuard(List<AppUserReadingProfile> profiles)
    {
        var anyOverlap = profiles
            .Any(rp => profiles
                .Where(other => other.Id != rp.Id)
                .Any(other => other.DeviceIds.Intersect(rp.DeviceIds).Any()));

        if (anyOverlap)
        {
            throw new KavitaException("reading-profiles-device-overlap");
        }
    }

    /// <summary>
    /// Deletes all implicit profiles with overlapping ids (For devices 0 overlaps with 0). And removes links with
    /// series & libraries
    /// </summary>
    /// <param name="profiles"></param>
    /// <param name="seriesIds"></param>
    /// <param name="libraryIds"></param>
    /// <param name="deviceIds"></param>
    private void DeleteImplicitAndRemoveFromUserProfiles(IList<AppUserReadingProfile> profiles, IList<int> seriesIds, IList<int> libraryIds, List<int>? deviceIds)
    {
        var implicitProfiles = profiles
            .Where(DeviceIdFilter)
            .Where(rp => rp.SeriesIds.Intersect(seriesIds).Any())
            .Where(rp => rp.Kind == ReadingProfileKind.Implicit)
            .ToList();

        unitOfWork.AppUserReadingProfileRepository.RemoveRange(implicitProfiles);

        var nonImplicitProfiles = profiles
            .Where(DeviceIdFilter)
            .Where(rp => rp.SeriesIds.Intersect(seriesIds).Any() || rp.LibraryIds.Intersect(libraryIds).Any())
            .Where(rp => rp.Kind != ReadingProfileKind.Implicit);

        foreach (var profile in nonImplicitProfiles)
        {
            profile.SeriesIds.RemoveAll(seriesIds.Contains);
            profile.LibraryIds.RemoveAll(libraryIds.Contains);

            unitOfWork.AppUserReadingProfileRepository.Update(profile);
        }

        return;

        bool DeviceIdFilter(AppUserReadingProfile rp)
        {
            // We should clean all
            if (deviceIds == null) return true;

            if (deviceIds.Count == 0 && rp.DeviceIds.Count == 0) return true;

            return rp.DeviceIds.Intersect(deviceIds).Any();
        }
    }

    public static void UpdateReaderProfileFields(AppUserReadingProfile existingProfile, UserReadingProfileDto dto, bool updateName = true)
    {
        if (updateName && !string.IsNullOrEmpty(dto.Name) && existingProfile.Name != dto.Name)
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
