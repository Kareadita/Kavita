using System;
using System.Threading.Tasks;
using API.Data;
using API.DTOs.Koreader;
using API.Helpers;

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
        var file = await _unitOfWork.MangaFileRepository.GetByHash(koreaderBookDto.Document);
        var userProgressDto = await _unitOfWork.AppUserProgressRepository.GetUserProgressDtoAsync(file.ChapterId, userId);
        KoreaderHelper.UpdateProgressDto(koreaderBookDto.Progress, userProgressDto);
        _readerService.SaveReadingProgress(userProgressDto, userId);
        await _unitOfWork.CommitAsync();
    }

    public async Task<KoreaderBookDto> GetProgress(string bookHash, int userId)
    {
        var file = await _unitOfWork.MangaFileRepository.GetByHash(bookHash);
        var progressDto = await _unitOfWork.AppUserProgressRepository.GetUserProgressDtoAsync(file.ChapterId, userId);
        var koreaderProgress = KoreaderHelper.GetKoreaderPosition(progressDto);
        return new KoreaderBookDto
        {
            Document = bookHash,
            Device_id = "kavita",
            Device = "kavita",
            Progress = koreaderProgress,
            Percentage = 0.5f
            // We can potentially calculate percentage later if needed.
        };
    }
}
