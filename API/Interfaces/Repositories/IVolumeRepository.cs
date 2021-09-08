using System.Collections.Generic;
using System.Threading.Tasks;
using API.DTOs;
using API.Entities;

namespace API.Interfaces.Repositories
{
    public interface IVolumeRepository
    {
        void Update(Volume volume);
        Task<Chapter> GetChapterAsync(int chapterId);
        Task<ChapterDto> GetChapterDtoAsync(int chapterId);
        Task<IList<MangaFile>> GetFilesForChapterAsync(int chapterId);
        Task<IList<MangaFile>> GetFilesForChaptersAsync(IReadOnlyList<int> chapterIds);
        Task<IList<Chapter>> GetChaptersAsync(int volumeId);
        Task<byte[]> GetChapterCoverImageAsync(int chapterId);
        Task<IList<MangaFile>> GetFilesForVolume(int volumeId);
    }
}
