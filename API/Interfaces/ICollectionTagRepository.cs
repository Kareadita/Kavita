using System.Collections.Generic;
using System.Threading.Tasks;
using API.DTOs;
using API.Entities;

namespace API.Interfaces
{
    public interface ICollectionTagRepository
    {
        void Remove(CollectionTag tag);
        Task<IEnumerable<CollectionTagDto>> GetAllTagDtosAsync();
        Task<IEnumerable<SeriesDto>> GetSeriesDtosForTagAsync(int tagId);
        Task<IEnumerable<Series>> GetSeriesForTagAsync(int tagId); 
        Task<IEnumerable<CollectionTagDto>> SearchTagDtosAsync(string searchQuery);

        Task<bool> DoesTagExist(string name);

        Task<byte[]> GetCoverImageAsync(int collectionTagId);
        Task<IEnumerable<CollectionTagDto>> GetAllPromotedTagDtosAsync();
        Task<CollectionTag> GetTagAsync(int tagId);
        Task<CollectionTag> GetFullTagAsync(int tagId);
    }
}