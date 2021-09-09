using System.Collections.Generic;
using System.Threading.Tasks;
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
    }
}
