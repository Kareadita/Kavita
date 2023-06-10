using API.DTOs.Scrobbling;
using API.Entities;
using API.Entities.Scrobble;

namespace API.Helpers.Builders;

public class ScrobbleEventBuilder : IEntityBuilder<ScrobbleEvent>
{
    private ScrobbleEvent _scrobbleEvent;
    public ScrobbleEvent Build() => _scrobbleEvent;

    public ScrobbleEventBuilder(int userId, int seriesId, int libraryId, MediaFormat format, ScrobbleEventType type)
    {
        // _scrobbleEvent = new ScrobbleEvent()
        // {
        //     ScrobbleEventType = type,
        //     Format = series.Format,
        //
        // }
    }
}
