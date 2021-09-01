using System.Collections.Generic;
using System.Threading.Tasks;
using API.DTOs.ReadingLists;

namespace API.Interfaces.Repositories
{
    public interface IReadingListRepository
    {
        Task<IEnumerable<ReadingListDto>> GetReadingListsForUser(int userId, bool includePromoted);
    }
}
