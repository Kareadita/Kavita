using System.Threading.Tasks;
using API.Data;
using API.DTOs.Koreader;
using API.DTOs.Progress;
using API.Extensions;
using API.Helpers;
using API.Helpers.Builders;
using API.Services.Reading;
using Kavita.Common;
using Microsoft.Extensions.Logging;

namespace API.Services;

#nullable enable

public interface IKoreaderService
{
    Task SaveProgress(KoreaderBookDto koreaderBookDto, int userId);
    Task<KoreaderBookDto> GetProgress(string bookHash, int userId);
}

public class KoreaderService : IKoreaderService
{
    private readonly IReaderService _readerService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILocalizationService _localizationService;
    private readonly ILogger<KoreaderService> _logger;

    public KoreaderService(IReaderService readerService, IUnitOfWork unitOfWork, ILocalizationService localizationService, ILogger<KoreaderService> logger)
    {
        _readerService = readerService;
        _unitOfWork = unitOfWork;
        _localizationService = localizationService;
        _logger = logger;
    }

    /// <summary>
    /// Given a Koreader hash, locate the underlying file and generate/update a progress event.
    /// </summary>
    /// <param name="koreaderBookDto"></param>
    /// <param name="userId"></param>
    public async Task SaveProgress(KoreaderBookDto koreaderBookDto, int userId)
    {
        _logger.LogDebug("Saving Koreader progress for User ({UserId}): {KoreaderProgress}", userId, koreaderBookDto.progress.Sanitize());
        var file = await _unitOfWork.MangaFileRepository.GetByKoreaderHash(koreaderBookDto.document);
        if (file == null) throw new KavitaException(await _localizationService.Translate(userId, "file-missing"));

        var userProgressDto = await _unitOfWork.AppUserProgressRepository.GetUserProgressDtoAsync(file.ChapterId, userId);
        if (userProgressDto == null)
        {
            var chapterDto = await _unitOfWork.ChapterRepository.GetChapterDtoAsync(file.ChapterId, userId);
            if (chapterDto == null) throw new KavitaException(await _localizationService.Translate(userId, "chapter-doesnt-exist"));

            var volumeDto = await _unitOfWork.VolumeRepository.GetVolumeByIdAsync(chapterDto.VolumeId);
            if (volumeDto == null) throw new KavitaException(await _localizationService.Translate(userId, "volume-doesnt-exist"));

            userProgressDto = new ProgressDto()
            {
                ChapterId = file.ChapterId,
                VolumeId = chapterDto.VolumeId,
                SeriesId = volumeDto.SeriesId,
            };
        }
        // Update the bookScrollId if possible
        var reportedProgress = koreaderBookDto.progress;
        KoreaderHelper.UpdateProgressDto(userProgressDto, koreaderBookDto.progress);
        _logger.LogDebug("Converting KOReader progress from {ReportedProgress} to {ScopedProgress}", reportedProgress.Sanitize(), userProgressDto.BookScrollId?.Sanitize());

        await _readerService.SaveReadingProgress(userProgressDto, userId);
    }

    /// <summary>
    /// Returns a Koreader Dto representing current book and the progress within
    /// </summary>
    /// <param name="bookHash"></param>
    /// <param name="userId"></param>
    /// <returns></returns>
    public async Task<KoreaderBookDto> GetProgress(string bookHash, int userId)
    {
        var settingsDto = await _unitOfWork.SettingsRepository.GetSettingsDtoAsync();

        var file = await _unitOfWork.MangaFileRepository.GetByKoreaderHash(bookHash);

        if (file == null) throw new KavitaException(await _localizationService.Translate(userId, "file-missing"));

        var progressDto = await _unitOfWork.AppUserProgressRepository.GetUserProgressDtoAsync(file.ChapterId, userId);
        var originalScrollId = progressDto?.BookScrollId;
        var koreaderProgress = KoreaderHelper.GetKoreaderPosition(progressDto);
        _logger.LogDebug("Converting KOReader progress from {KavitaProgress} to {KOReaderProgress}", originalScrollId?.Sanitize() ?? string.Empty, progressDto?.BookScrollId?.Sanitize() ?? string.Empty);


        return new KoreaderBookDtoBuilder(bookHash).WithProgress(koreaderProgress)
            .WithPercentage(progressDto?.PageNum, file.Pages)
            .WithDeviceId(settingsDto.InstallId, userId)
            .WithTimestamp(progressDto?.LastModifiedUtc)
            .Build();
    }
}
