using System.Linq;
using Kavita.Models.Entities.Interfaces;

namespace Kavita.Database.Extensions;

public static class CoverImageFileExtensions
{
    public static bool HasCoverImage(this IHasCoverImageFile entity)
    {
        return entity.CoverImageFileId != null;
    }
}
