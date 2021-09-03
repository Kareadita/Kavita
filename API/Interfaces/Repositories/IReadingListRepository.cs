using System.Collections.Generic;
using System.Threading.Tasks;
using API.DTOs.ReadingLists;
using API.Entities;
using API.Helpers;

namespace API.Interfaces.Repositories
{
    public interface IReadingListRepository
    {
        Task<PagedList<ReadingListDto>> GetReadingListDtosForUserAsync(int userId, bool includePromoted, UserParams userParams);
        Task<ReadingList> GetReadingListByIdAsync(int readingListId);
        Task<IEnumerable<ReadingListItemDto>> GetReadingListItemDtosByIdAsync(int readingListId, int userId);
        Task<ReadingListDto> GetReadingListDtoByIdAsync(int readingListId, int userId);
        Task<IEnumerable<ReadingListItemDto>> AddReadingProgressModifiers(int userId, IList<ReadingListItemDto> items);
        Task<ReadingListDto> GetReadingListDtoByTitleAsync(string title);
    }
}
