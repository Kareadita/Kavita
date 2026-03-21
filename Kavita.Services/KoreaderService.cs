using System.Threading;
using System.Threading.Tasks;
using Kavita.API.Database;
using Kavita.API.Services;
using Kavita.API.Services.Reading;
using Kavita.Common;
using Kavita.Common.Extensions;
using Kavita.Models.Builders;
using Kavita.Models.DTOs.Koreader;
using Kavita.Models.DTOs.Progress;
using Kavita.Models.Entities.Enums;
using Kavita.Services.Helpers;
using Microsoft.Extensions.Logging;

namespace Kavita.Services;

public class KoreaderService(
    IReaderService readerService,
    IUnitOfWork unitOfWork,
    ILocalizationService localizationService,
    ILogger<KoreaderService> logger)
    : IKoreaderService
{
    /// <summary>
    /// Given a Koreader hash, locate the underlying file and generate/update a progress event.
    /// </summary>
    /// <param name="koreaderBookDto"></param>
    /// <param name="userId"></param>
    /// <param name="ct"></param>
    public async Task SaveProgress(KoreaderBookDto koreaderBookDto, int userId, CancellationToken ct = default)
    {
        logger.LogDebug("Saving KOReader progress for User ({UserId}): {KoreaderProgress} - {KoreaderHash}",
            userId, koreaderBookDto.progress.Sanitize(), koreaderBookDto.document.Sanitize());
        var file = await unitOfWork.MangaFileRepository.GetByKoreaderHash(koreaderBookDto.document, ct);
        if (file == null)
        {
            logger.LogWarning("KOReader progress for unknown book: {BookHash}. Run a force scan on the series to generate KOReader hashes", koreaderBookDto.document.Sanitize());
            throw new KavitaException(await localizationService.Translate(userId, "file-missing"));
        }

        var userProgressDto = await unitOfWork.AppUserProgressRepository.GetUserProgressDtoAsync(file.ChapterId, userId, ct);
        if (userProgressDto == null)
        {
            var chapterDto = await unitOfWork.ChapterRepository.GetChapterDtoAsync(file.ChapterId, userId, ct);
            if (chapterDto == null) throw new KavitaException(await localizationService.Translate(userId, "chapter-doesnt-exist"));

            var volumeDto = await unitOfWork.VolumeRepository.GetVolumeByIdAsync(chapterDto.VolumeId, ct: ct);
            if (volumeDto == null) throw new KavitaException(await localizationService.Translate(userId, "volume-doesnt-exist"));

            var seriesDto = await unitOfWork.SeriesRepository.GetSeriesDtoByIdAsync(volumeDto.SeriesId, userId, ct);
            if (seriesDto == null) throw new KavitaException(await localizationService.Translate(userId, "series-doesnt-exist"));

            userProgressDto = new ProgressDto()
            {
                PageNum = 0, // This is updated in KoreaderHelper.UpdateProgressDto
                ChapterId = file.ChapterId,
                VolumeId = chapterDto.VolumeId,
                SeriesId = seriesDto.Id,
                LibraryId = seriesDto.LibraryId
            };
        }

        // Update the bookScrollId if possible
        var reportedProgress = koreaderBookDto.progress;
        KoreaderHelper.UpdateProgressDto(userProgressDto, koreaderBookDto.progress);

        logger.LogDebug("Converted KOReader progress from {ProgressEncoding} to Page {PageNum} with ScrollId: {ScrollId}. For Chapter {ChapterId} in Series {SeriesId}",
            reportedProgress.Sanitize(), userProgressDto.PageNum, userProgressDto.BookScrollId?.Sanitize() ?? string.Empty,
            userProgressDto.ChapterId, userProgressDto.SeriesId);

        // Normal saving from kavita will be //body/h2[1]
        await readerService.SaveReadingProgress(userProgressDto, userId);
    }

    /// <summary>
    /// Returns a Koreader Dto representing the current book and the progress within
    /// </summary>
    /// <param name="bookHash"></param>
    /// <param name="userId"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    public async Task<KoreaderBookDto> GetProgress(string bookHash, int userId, CancellationToken ct = default)
    {
        var settingsDto = await unitOfWork.SettingsRepository.GetSettingsDtoAsync(ct);

        var file = await unitOfWork.MangaFileRepository.GetByKoreaderHash(bookHash, ct);
        if (file == null)
        {
            logger.LogWarning("KOReader progress for unknown book: {BookHash}. Run a force scan on the series to generate KOReader hashes", bookHash.Sanitize());
            throw new KavitaException(await localizationService.Translate(userId, "file-missing"));
        }

        var progressDto = await unitOfWork.AppUserProgressRepository.GetUserProgressDtoAsync(file.ChapterId, userId, ct);

        // Non-epubs use the pageNum as the progress. KOReader is 1-index based
        var koreaderProgress = $"{progressDto?.PageNum + 1 ?? 0}";
        if (file.Format == MangaFormat.Epub)
        {
            koreaderProgress = KoreaderHelper.GetKoreaderPosition(progressDto);
        }

        var response = new KoreaderBookDtoBuilder(bookHash)
            .WithProgress(koreaderProgress)
            .WithPercentage(progressDto?.PageNum, file.Pages)
            .WithDeviceId(settingsDto.InstallId, userId)
            .WithTimestamp(progressDto?.LastModifiedUtc)
            .Build();

        logger.LogDebug("Responding to KOReader with Page {PageNum}, Scroll Id: {ScrollId}, and Progress: {Progress}",
            progressDto?.PageNum, response.progress.Sanitize(), response.percentage);


        return response;
    }
}
