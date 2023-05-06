using API.Entities;

namespace API.Helpers.Builders;

public class ReadingListItemBuilder : IEntityBuilder<ReadingListItem>
{
    private readonly ReadingListItem _item;
    public ReadingListItem Build() => _item;

    public ReadingListItemBuilder(int index, int seriesId, int volumeId, int chapterId)
    {
        _item = new ReadingListItem()
        {
            Order = index,
            ChapterId = chapterId,
            SeriesId = seriesId,
            VolumeId = volumeId
        };

    }
}
