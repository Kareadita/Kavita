using System.Collections.Generic;
using System.Threading.Tasks;
using Kavita.Models.DTOs.Collection;
using Kavita.Models.Entities.User;

namespace Kavita.API.Services;

public interface ICollectionTagService
{
    Task<bool> DeleteTag(int tagId, AppUser user);
    Task<bool> UpdateTag(AppUserCollectionDto dto, int userId);
    Task<bool> RemoveTagFromSeries(AppUserCollection? tag, IEnumerable<int> seriesIds);
}
