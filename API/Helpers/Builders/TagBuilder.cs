using System.Collections.Generic;
using API.Entities;
using API.Entities.Metadata;
using API.Extensions;

namespace API.Helpers.Builders;

public class TagBuilder : IEntityBuilder<Tag>
{
    private readonly Tag _tag;
    public Tag Build() => _tag;

    public TagBuilder(string name)
    {
        _tag = new Tag()
        {
            Title = name.Trim().SentenceCase(),
            NormalizedTitle = name.ToNormalized(),
            Chapters = new List<Chapter>(),
            SeriesMetadatas = new List<SeriesMetadata>()
        };
    }

    public TagBuilder WithSeriesMetadata(SeriesMetadata seriesMetadata)
    {
        _tag.SeriesMetadatas ??= new List<SeriesMetadata>();
        _tag.SeriesMetadatas.Add(seriesMetadata);
        return this;
    }
}
