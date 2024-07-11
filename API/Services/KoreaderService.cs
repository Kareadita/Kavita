using System;
using System.Threading.Tasks;
using API.Data;
using API.DTOs.Koreader;

namespace API.Services;

#nullable enable

public interface IKoreaderService
{
    Task SaveProgress(double progress, string bookHash, int userId);
    Task<double> GetProgress(string bookHash, int userId);
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

    public async Task SaveProgress(double progress, string bookHash, int userId)
    {
        var file = await _unitOfWork.MangaFileRepository.GetByHash(bookHash);
        var userProgress = await _unitOfWork.AppUserProgressRepository.GetUserProgressAsync(file.ChapterId, userId);
        userProgress.PagesRead = (int)Math.Floor(progress * file.Pages);
        userProgress.MarkModified();
        _unitOfWork.AppUserProgressRepository.Update(userProgress);
        await _unitOfWork.CommitAsync();
    }

    public async Task<double> GetProgress(string bookHash, int userId)
    {
        var file = await _unitOfWork.MangaFileRepository.GetByHash(bookHash);
        var progressDto = await _unitOfWork.AppUserProgressRepository.GetUserProgressDtoAsync(file.ChapterId, userId);

        var chapterProgress = (double)progressDto.PageNum / (double)file.Pages;
        return chapterProgress;
    }
}
