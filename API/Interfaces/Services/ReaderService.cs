
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using API.Data.Repositories;
using API.DTOs;
using API.Entities;

namespace API.Interfaces.Services
{
    public class ReaderService : IReaderService
    {
        private readonly IUnitOfWork _unitOfWork;

        public ReaderService(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        /// <summary>
        /// Saves progress to DB
        /// </summary>
        /// <param name="progressDto"></param>
        /// <param name="userId"></param>
        /// <returns></returns>
        public async Task<bool> SaveReadingProgress(ProgressDto progressDto, int userId)
        {
            // Don't let user save past total pages.
            progressDto.PageNum = await CapPageToChapter(progressDto.ChapterId, progressDto.PageNum);

            try
            {
                var userProgress =
                    await _unitOfWork.AppUserProgressRepository.GetUserProgressAsync(progressDto.ChapterId, userId);

                if (userProgress == null)
                {
                    // Create a user object
                    var userWithProgress = await _unitOfWork.UserRepository.GetUserByIdAsync(userId, AppUserIncludes.Progress);
                    userWithProgress.Progresses ??= new List<AppUserProgress>();
                    userWithProgress.Progresses.Add(new AppUserProgress
                    {
                        PagesRead = progressDto.PageNum,
                        VolumeId = progressDto.VolumeId,
                        SeriesId = progressDto.SeriesId,
                        ChapterId = progressDto.ChapterId,
                        BookScrollId = progressDto.BookScrollId,
                        LastModified = DateTime.Now
                    });
                    _unitOfWork.UserRepository.Update(userWithProgress);
                }
                else
                {
                    userProgress.PagesRead = progressDto.PageNum;
                    userProgress.SeriesId = progressDto.SeriesId;
                    userProgress.VolumeId = progressDto.VolumeId;
                    userProgress.BookScrollId = progressDto.BookScrollId;
                    userProgress.LastModified = DateTime.Now;
                    _unitOfWork.AppUserProgressRepository.Update(userProgress);
                }

                if (await _unitOfWork.CommitAsync())
                {
                    return true;
                }
            }
            catch (Exception)
            {
                await _unitOfWork.RollbackAsync();
            }

            return false;
        }

        public async Task<int> CapPageToChapter(int chapterId, int page)
        {
            var totalPages = await _unitOfWork.ChapterRepository.GetChapterTotalPagesAsync(chapterId);
            if (page > totalPages)
            {
                page = totalPages;
            }

            if (page < 0)
            {
                page = 0;
            }

            return page;
        }
    }
}
