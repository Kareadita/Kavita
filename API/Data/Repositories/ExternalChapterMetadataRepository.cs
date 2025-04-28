#nullable enable
using System.Collections.Generic;
using System.Threading.Tasks;
using API.Entities.Metadata;
using API.Extensions.QueryExtensions;
using AutoMapper;
using Microsoft.EntityFrameworkCore;

namespace API.Data.Repositories;

public enum ExternalChapterMetadataIncludes
{
    None = 0,
    ExternalReviews = 1 << 1,
}

public interface IExternalChapterMetadataRepository
{
    void Attach(ExternalChapterMetadata externalChapterMetadata);
    void Remove(IEnumerable<ExternalChapterReview>? reviews);

    Task<ExternalChapterMetadata?> Get(int chapterId, ExternalChapterMetadataIncludes includes = ExternalChapterMetadataIncludes.ExternalReviews);
}

public class ExternalChapterMetadataRepository(DataContext context, IMapper mapper): IExternalChapterMetadataRepository
{

    public void Attach(ExternalChapterMetadata externalChapterMetadata)
    {
        context.ExternalChapterMetadata.Attach(externalChapterMetadata);
    }
    public void Remove(IEnumerable<ExternalChapterReview>? reviews)
    {
        if (reviews == null) return;
        context.ExternalChapterReview.RemoveRange(reviews);

    }
    public async Task<ExternalChapterMetadata?> Get(int chapterId, ExternalChapterMetadataIncludes includes = ExternalChapterMetadataIncludes.ExternalReviews)
    {
        return await context.ExternalChapterMetadata
            .Includes(includes)
            .FirstOrDefaultAsync(c => c.ChapterId == chapterId);
    }
}
