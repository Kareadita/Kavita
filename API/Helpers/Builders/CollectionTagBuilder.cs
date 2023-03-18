using System.Collections.Generic;
using API.Entities;
using API.Entities.Metadata;
using API.Extensions;

namespace API.Helpers.Builders;

public class CollectionTagBuilder : IEntityBuilder<CollectionTag>
{
    private readonly CollectionTag _collectionTag;
    public CollectionTag Build() => _collectionTag;

    public CollectionTagBuilder(string title, bool promoted = false)
    {
        title = title.Trim();
        _collectionTag = new CollectionTag()
        {
            Id = 0,
            NormalizedTitle = title.ToNormalized(),
            Title = title,
            Promoted = promoted,
            Summary = string.Empty,
            SeriesMetadatas = new List<SeriesMetadata>()
        };
    }

    public CollectionTagBuilder WithId(int id)
    {
        _collectionTag.Id = id;
        return this;
    }

    public CollectionTagBuilder WithSummary(string summary)
    {
        _collectionTag.Summary = summary;
        return this;
    }

    public CollectionTagBuilder WithIsPromoted(bool promoted)
    {
        _collectionTag.Promoted = promoted;
        return this;
    }

    public CollectionTagBuilder WithSeriesMetadata(SeriesMetadata seriesMetadata)
    {
        _collectionTag.SeriesMetadatas ??= new List<SeriesMetadata>();
        _collectionTag.SeriesMetadatas.Add(seriesMetadata);
        return this;
    }
}
