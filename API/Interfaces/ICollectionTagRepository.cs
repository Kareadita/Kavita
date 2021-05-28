using System.Collections.Generic;
using System.Threading.Tasks;
using API.DTOs;

namespace API.Interfaces
{
    public interface ICollectionTagRepository
    {
        Task<IEnumerable<CollectionTagDto>> GetAllTagDtosAsync();

        Task<IEnumerable<SeriesDto>> GetSeriesForTag(int tagId); // Should this be tag name?
        Task<IEnumerable<CollectionTagDto>> SearchTagDtosAsync(string searchQuery);

        Task<bool> DoesTagExist(string name);

        Task<byte[]> GetCoverImageAsync(int collectionTagId);
        Task<IEnumerable<CollectionTagDto>> GetAllPromotedTagDtosAsync();
    }
}