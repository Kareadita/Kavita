using System.Collections.Generic;
using Kavita.Common.Extensions;
using Kavita.Models.Entities;
using Kavita.Models.Entities.Metadata;

namespace Kavita.Models.Builders;

public class TagBuilder : IEntityBuilder<Tag>
{
    private readonly Tag _tag;
    public Tag Build() => _tag;

    public TagBuilder(string name)
    {
        _tag = new Tag()
        {
            Title = name.Trim(),
            NormalizedTitle = name.ToNormalized(),
            Chapters = [],
            SeriesMetadatas = []
        };
    }

    public TagBuilder WithSeriesMetadata(SeriesMetadata seriesMetadata)
    {
        _tag.SeriesMetadatas ??= new List<SeriesMetadata>();
        _tag.SeriesMetadatas.Add(seriesMetadata);
        return this;
    }
}
