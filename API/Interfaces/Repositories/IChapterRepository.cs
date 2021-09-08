using System.Collections.Generic;
using System.Threading.Tasks;
using API.DTOs;
using API.Entities;

namespace API.Interfaces.Repositories
{
    public interface IChapterRepository
    {
        void Update(Chapter chapter);
        Task<IEnumerable<Chapter>> GetChaptersByIdsAsync(IList<int> chapterIds);
    }
}
