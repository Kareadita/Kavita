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
            Chapters = [],
            SeriesMetadatas = []
        };
    }

    public GenreBuilder WithSeriesMetadata(SeriesMetadata seriesMetadata)
    {
        _genre.SeriesMetadatas ??= [];
        _genre.SeriesMetadatas.Add(seriesMetadata);
        return this;
    }
}
