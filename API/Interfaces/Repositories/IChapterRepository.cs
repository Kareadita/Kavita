using System.Collections.Generic;
using System.Threading.Tasks;
using API.DTOs;
using API.DTOs.Reader;
using API.Entities;

namespace API.Interfaces.Repositories
{
    public interface IChapterRepository
    {
        void Update(Chapter chapter);
        Task<IEnumerable<Chapter>> GetChaptersByIdsAsync(IList<int> chapterIds);
        Task<IChapterInfoDto> GetChapterInfoDtoAsync(int chapterId);
        Task<int> GetChapterTotalPagesAsync(int chapterId);
        Task<Chapter> GetChapterAsync(int chapterId);
        Task<ChapterDto> GetChapterDtoAsync(int chapterId);
        Task<IList<MangaFile>> GetFilesForChapterAsync(int chapterId);
        Task<IList<Chapter>> GetChaptersAsync(int volumeId);
        Task<IList<MangaFile>> GetFilesForChaptersAsync(IReadOnlyList<int> chapterIds);
        Task<byte[]> GetChapterCoverImageAsync(int chapterId);
    }
}
