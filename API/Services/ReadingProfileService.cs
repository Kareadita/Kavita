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
    /// Series -> Library -> Default
    /// </summary>
    /// <param name="userId"></param>
    /// <param name="series"></param>
    /// <returns></returns>
    Task<AppUserReadingProfile> GetReadingProfileForSeries(int userId, Series series);

    /// <summary>
    /// Updates, or adds a specific reading profile for a user
    /// </summary>
    /// <param name="userId"></param>
    /// <param name="dto"></param>
    /// <returns></returns>
    Task<bool> UpdateReadingProfile(int userId, UserReadingProfileDto dto);

    /// <summary>
    /// Updates, or adds a specific reading profile for a user for a specific series.
    /// If the reading profile is not assigned to the given series (Library, Default fallback),
    /// a new implicit reading profile is created
    /// </summary>
    /// <param name="userId"></param>
    /// <param name="seriesId"></param>
    /// <param name="dto"></param>
    /// <returns></returns>
    Task<bool> UpdateReadingProfileForSeries(int userId, int seriesId, UserReadingProfileDto dto);

    /// <summary>
    /// Deletes the implicit reading profile for a given series, if it exists
    /// </summary>
    /// <param name="userId"></param>
    /// <param name="seriesId"></param>
    /// <returns></returns>
    Task DeleteImplicitForSeries(int userId, int seriesId);

    /// <summary>
    /// Deletes a given profile for a user
    /// </summary>
    /// <param name="userId"></param>
    /// <param name="profileId"></param>
    /// <returns></returns>
    /// <exception cref="UnauthorizedAccessException"></exception>
    /// <exception cref="KavitaException">The default profile for the user cannot be deleted</exception>
    Task DeleteReadingProfile(int userId, int profileId);

}

public class ReadingProfileService(IUnitOfWork unitOfWork): IReadingProfileService
{

    public async Task<AppUserReadingProfile> GetReadingProfileForSeries(int userId, Series series)
    {
        var seriesProfile = await unitOfWork.AppUserReadingProfileRepository.GetProfileForSeries(userId, series.Id);
        if (seriesProfile != null) return seriesProfile;

        var libraryProfile = await unitOfWork.AppUserReadingProfileRepository.GetProfileForLibrary(userId, series.Id);
        if (libraryProfile != null) return libraryProfile;

        var user = await unitOfWork.UserRepository.GetUserByIdAsync(userId);
        if (user == null) throw new UnauthorizedAccessException();

        return await unitOfWork.AppUserReadingProfileRepository.GetProfile(user.UserPreferences.DefaultReadingProfileId);
    }

    public async Task<bool> UpdateReadingProfile(int userId, UserReadingProfileDto dto)
    {
        var existingProfile = await unitOfWork.AppUserReadingProfileRepository.GetProfile(dto.Id);
        if (existingProfile == null)
        {
            existingProfile = new AppUserReadingProfileBuilder(userId).Build();
        }

        if (existingProfile.UserId != userId) return false;

        UpdateReaderProfileFields(existingProfile, dto);
        unitOfWork.AppUserReadingProfileRepository.Update(existingProfile);
        return await unitOfWork.CommitAsync();
    }

    public async Task<bool> UpdateReadingProfileForSeries(int userId, int seriesId, UserReadingProfileDto dto)
    {
        var existingProfile = await unitOfWork.AppUserReadingProfileRepository.GetProfile(dto.Id);

        // TODO: Rewrite
        var isNew = false;
        if (existingProfile == null || existingProfile.Series.All(s => s.Id != seriesId))
        {
            var series = await unitOfWork.SeriesRepository.GetSeriesByIdAsync(seriesId);
            if (series == null) throw new KeyNotFoundException();

            existingProfile = new AppUserReadingProfileBuilder(userId)
                .WithSeries(series)
                .WithImplicit(true)
                .Build();

            isNew = true;
        }

        if (existingProfile.UserId != userId) return false;

        UpdateReaderProfileFields(existingProfile, dto);

        if (isNew)
        {
            unitOfWork.AppUserReadingProfileRepository.Add(existingProfile);
        }
        else
        {
            unitOfWork.AppUserReadingProfileRepository.Update(existingProfile);
        }

        return await unitOfWork.CommitAsync();
    }

    public async Task DeleteImplicitForSeries(int userId, int seriesId)
    {
        var profile = await unitOfWork.AppUserReadingProfileRepository.GetProfileForSeries(userId, seriesId);
        if (profile == null) return;

        if (!profile.Implicit) return;

        profile.Series = profile.Series.Where(s => s.Id != seriesId).ToList();

        await unitOfWork.CommitAsync();
    }

    public async Task DeleteReadingProfile(int userId, int profileId)
    {
        var profile = await unitOfWork.AppUserReadingProfileRepository.GetProfile(profileId);
        if (profile == null) throw new KeyNotFoundException();

        var user = await unitOfWork.UserRepository.GetUserByIdAsync(userId, AppUserIncludes.UserPreferences);
        if (user == null || profile.UserId != userId) throw new UnauthorizedAccessException();

        if (user.UserPreferences.DefaultReadingProfileId == profileId) throw new KavitaException("cant-delete-default-profile");

        unitOfWork.AppUserReadingProfileRepository.Remove(profile);
        await unitOfWork.CommitAsync();
    }

    private static void UpdateReaderProfileFields(AppUserReadingProfile existingProfile, UserReadingProfileDto dto)
    {
        if (!string.IsNullOrEmpty(dto.Name) && existingProfile.NormalizedName != dto.Name.ToNormalized())
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
