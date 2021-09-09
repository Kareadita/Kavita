
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
        /// <param name="user"></param>
        /// <returns></returns>
        public async Task<bool> SaveReadingProgress(ProgressDto progressDto, int userId)
        {
            // TODO: Refactor this to check if Progress item exists and update, else pull the user progresses.
            // Aka optimize for the common path. Creating a new progress only happens once

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

                //_unitOfWork.UserRepository.Update(user);

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
