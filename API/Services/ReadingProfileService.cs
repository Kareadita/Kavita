using System;
using System.Linq;
using System.Threading.Tasks;
using API.Data;
using API.DTOs;
using API.Entities;

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
            existingProfile = new AppUserReadingProfile
            {
                UserId = userId,
            };
        }

        if (existingProfile.UserId != userId) return false;

        UpdateReaderProfileFields(existingProfile, dto);
        unitOfWork.AppUserReadingProfileRepository.Update(existingProfile);
        return await unitOfWork.CommitAsync();
    }

    public async Task<bool> UpdateReadingProfileForSeries(int userId, int seriesId, UserReadingProfileDto dto)
    {
        var existingProfile = await unitOfWork.AppUserReadingProfileRepository.GetProfile(dto.Id);

        if (existingProfile == null || existingProfile.Series.All(s => s.Id != seriesId))
        {
            existingProfile = new AppUserReadingProfile
            {
                Implicit = true,
                UserId = userId,
            };
        }

        if (existingProfile.UserId != userId) return false;

        UpdateReaderProfileFields(existingProfile, dto);
        unitOfWork.AppUserReadingProfileRepository.Update(existingProfile);
        return await unitOfWork.CommitAsync();
    }

    public async Task DeleteImplicitForSeries(int userId, int seriesId)
    {
        var profile = await unitOfWork.AppUserReadingProfileRepository.GetProfileForSeries(userId, seriesId);
        if (profile == null) return;

        if (!profile.Implicit) return;

        unitOfWork.AppUserReadingProfileRepository.Remove(profile);
        await unitOfWork.CommitAsync();
    }

    private static void UpdateReaderProfileFields(AppUserReadingProfile existingProfile, UserReadingProfileDto dto)
    {
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
