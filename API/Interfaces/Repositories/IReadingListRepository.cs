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
        Task<IEnumerable<ReadingListItemDto>> GetReadingListItemDtosByIdAsync(int readingListId, int userId);
        Task<ReadingListDto> GetReadingListDtoByIdAsync(int readingListId, int userId);
        Task<IEnumerable<ReadingListItemDto>> AddReadingProgressModifiers(int userId, IList<ReadingListItemDto> items);
    }
}
