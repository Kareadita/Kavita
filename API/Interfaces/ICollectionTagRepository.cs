using System.Collections.Generic;
using System.Threading.Tasks;
using API.DTOs;

namespace API.Interfaces
{
    public interface ICollectionTagRepository
    {
        Task<IEnumerable<CollectionTagDto>> GetAllTagDtos();

        Task<IEnumerable<SeriesDto>> GetSeriesForTag(int tagId); // Should this be tag name?
        Task<IEnumerable<CollectionTagDto>> SearchTagDtos(string searchQuery);

        Task<bool> DoesTagExist(string name);
        
    }
}