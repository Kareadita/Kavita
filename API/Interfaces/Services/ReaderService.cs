
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
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
        /// <param name="user"></param>
        /// <returns></returns>
        public async Task<bool> SaveReadingProgress(ProgressDto progressDto, AppUser user)
        {
            // Don't let user save past total pages.
            var chapter = await _unitOfWork.VolumeRepository.GetChapterAsync(progressDto.ChapterId);
            if (progressDto.PageNum > chapter.Pages)
            {
                progressDto.PageNum = chapter.Pages;
            }

            if (progressDto.PageNum < 0)
            {
                progressDto.PageNum = 0;
            }

            try
            {
                user.Progresses ??= new List<AppUserProgress>();
                var userProgress =
                    user.Progresses.FirstOrDefault(x => x.ChapterId == progressDto.ChapterId && x.AppUserId == user.Id);

                if (userProgress == null)
                {
                    user.Progresses.Add(new AppUserProgress
                    {
                        PagesRead = progressDto.PageNum,
                        VolumeId = progressDto.VolumeId,
                        SeriesId = progressDto.SeriesId,
                        ChapterId = progressDto.ChapterId,
                        BookScrollId = progressDto.BookScrollId,
                        LastModified = DateTime.Now
                    });
                }
                else
                {
                    userProgress.PagesRead = progressDto.PageNum;
                    userProgress.SeriesId = progressDto.SeriesId;
                    userProgress.VolumeId = progressDto.VolumeId;
                    userProgress.BookScrollId = progressDto.BookScrollId;
                    userProgress.LastModified = DateTime.Now;
                }

                _unitOfWork.UserRepository.Update(user);

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
    }
}
