using System.Collections.Generic;
using System.Threading.Tasks;
using API.DTOs.CollectionTags;
using API.Entities;

namespace API.Interfaces.Repositories
{
    public interface ICollectionTagRepository
    {
        void Add(CollectionTag tag);
        void Remove(CollectionTag tag);
        Task<IEnumerable<CollectionTagDto>> GetAllTagDtosAsync();
        Task<IEnumerable<CollectionTagDto>> SearchTagDtosAsync(string searchQuery);
        Task<string> GetCoverImageAsync(int collectionTagId);
        Task<IEnumerable<CollectionTagDto>> GetAllPromotedTagDtosAsync();
        Task<CollectionTag> GetTagAsync(int tagId);
        Task<CollectionTag> GetFullTagAsync(int tagId);
        void Update(CollectionTag tag);
        Task<int> RemoveTagsWithoutSeries();
        Task<IEnumerable<CollectionTag>> GetAllTagsAsync();
        Task<IList<string>> GetAllCoverImagesAsync();
    }
}
