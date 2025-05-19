using System.Threading.Tasks;
using API.Data;
using API.DTOs.Koreader;
using API.DTOs.Progress;
using API.Helpers;
using API.Helpers.Builders;
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
    private readonly ILogger<KoreaderService> _logger;

    public KoreaderService(IReaderService readerService, IUnitOfWork unitOfWork, ILogger<KoreaderService> logger)
    {
        _readerService = readerService;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    /// <summary>
    /// Given a Koreader hash, locate the underlying file and generate/update a progress event.
    /// </summary>
    /// <param name="koreaderBookDto"></param>
    /// <param name="userId"></param>
    public async Task SaveProgress(KoreaderBookDto koreaderBookDto, int userId)
    {
        _logger.LogDebug("Saving Koreader progress for {UserId}: {KoreaderProgress}", userId, koreaderBookDto.Progress);
        var file = await _unitOfWork.MangaFileRepository.GetByKoreaderHash(koreaderBookDto.Document);
        if (file == null) return;

        var userProgressDto = await _unitOfWork.AppUserProgressRepository.GetUserProgressDtoAsync(file.ChapterId, userId);
        if (userProgressDto == null)
        {
            // TODO: Handle this case
            userProgressDto = new ProgressDto()
            {
                ChapterId = file.ChapterId,
            };
        }
        // Update the bookScrollId if possible
        KoreaderHelper.UpdateProgressDto(userProgressDto, koreaderBookDto.Progress);

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
        var builder = new KoreaderBookDtoBuilder(bookHash);

        var file = await _unitOfWork.MangaFileRepository.GetByKoreaderHash(bookHash);

        // TODO: How do we handle when file isn't found by hash?
        if (file == null) return builder.Build();

        var progressDto = await _unitOfWork.AppUserProgressRepository.GetUserProgressDtoAsync(file.ChapterId, userId);
        var koreaderProgress = KoreaderHelper.GetKoreaderPosition(progressDto);

        return builder.WithProgress(koreaderProgress)
            .WithPercentage(progressDto?.PageNum, file.Pages)
            .WithDeviceId(settingsDto.InstallId, userId) // TODO: Should we generate a hash for UserId + InstallId so that this DeviceId is unique to the user on the server?
            .Build();
    }
}
