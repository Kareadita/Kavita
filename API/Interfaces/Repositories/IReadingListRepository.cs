using System.Collections.Generic;
using System.Threading.Tasks;
using API.DTOs.ReadingLists;
using API.Entities;

namespace API.Interfaces.Repositories
{
    public interface IReadingListRepository
    {
        Task<IEnumerable<ReadingListDto>> GetReadingListDtosForUserAsync(int userId, bool includePromoted);
        Task<ReadingList> GetReadingListByIdAsync(int readingListId);
    }
}
