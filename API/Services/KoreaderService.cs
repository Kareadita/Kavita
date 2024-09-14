using System;
using System.Threading.Tasks;
using API.Data;
using API.Data.Migrations;
using API.DTOs.Koreader;
using API.Entities;
using API.Helpers;
using Microsoft.AspNetCore.Hosting.Server.Features;

namespace API.Services;

#nullable enable

public interface IKoreaderService
{
    Task SaveProgress(KoreaderBookDto koreaderBookDto, int userId);
    Task<KoreaderBookDto> GetProgress(string bookHash, int userId);
}

public class KoreaderService : IKoreaderService
{
    private IReaderService _readerService;
    private IUnitOfWork _unitOfWork;

    public KoreaderService(IReaderService readerService, IUnitOfWork unitOfWork)
    {
        _readerService = readerService;
        _unitOfWork = unitOfWork;
    }

    public async Task SaveProgress(KoreaderBookDto koreaderBookDto, int userId)
    {
        var file = await _unitOfWork.MangaFileRepository.GetByKoreaderHash(koreaderBookDto.Document);
        var userProgressDto = await _unitOfWork.AppUserProgressRepository.GetUserProgressDtoAsync(file.ChapterId, userId);

        KoreaderHelper.UpdateProgressDto(koreaderBookDto.Progress, userProgressDto);
        await _readerService.SaveReadingProgress(userProgressDto, userId);

        await _unitOfWork.CommitAsync();
    }

    public async Task<KoreaderBookDto> GetProgress(string bookHash, int userId)
    {
        var file = await _unitOfWork.MangaFileRepository.GetByKoreaderHash(bookHash);
        var progressDto = await _unitOfWork.AppUserProgressRepository.GetUserProgressDtoAsync(file.ChapterId, userId);
        var koreaderProgress = KoreaderHelper.GetKoreaderPosition(progressDto);
        var settingsDto = await _unitOfWork.SettingsRepository.GetSettingsDtoAsync();

        return new KoreaderBookDto
        {
            Document = bookHash,
            Device_id = settingsDto.InstallId,
            Device = "Kavita",
            Progress = koreaderProgress,
            Percentage = 0.5f
            // We can potentially calculate percentage later if needed.
        };
    }
}
