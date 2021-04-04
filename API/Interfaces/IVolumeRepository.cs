﻿using System.Collections.Generic;
using System.Threading.Tasks;
using API.DTOs;
using API.Entities;

namespace API.Interfaces
{
    public interface IVolumeRepository
    {
        void Update(Volume volume);
        Task<Chapter> GetChapterAsync(int chapterId);
        Task<ChapterDto> GetChapterDtoAsync(int chapterId);
        Task<IList<MangaFile>> GetFilesForChapter(int chapterId);
        IList<MangaFile> GetFilesForSeries(int seriesId);
        Task<IList<Chapter>> GetChaptersAsync(int volumeId);
        Task<byte[]> GetChapterCoverImageAsync(int chapterId);
    }
}