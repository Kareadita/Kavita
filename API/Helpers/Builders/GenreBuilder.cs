using System.Collections.Generic;
using API.Entities;
using API.Entities.Metadata;
using API.Extensions;

namespace API.Helpers.Builders;

public class GenreBuilder : IEntityBuilder<Genre>
{
    private readonly Genre _genre;
    public Genre Build() => _genre;

    public GenreBuilder(string name)
    {
        _genre = new Genre()
        {
            Title = name.Trim().SentenceCase(),
            NormalizedTitle = name.ToNormalized(),
            Chapters = new List<Chapter>(),
            SeriesMetadatas = new List<SeriesMetadata>()
        };
    }

    public GenreBuilder WithSeriesMetadata(SeriesMetadata seriesMetadata)
    {
        _genre.SeriesMetadatas ??= new List<SeriesMetadata>();
        _genre.SeriesMetadatas.Add(seriesMetadata);
        return this;
    }
}
