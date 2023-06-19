using API.Entities.Scrobble;

namespace API.Helpers.Builders;

public class ScrobbleHoldBuilder : IEntityBuilder<ScrobbleHold>
{
    private ScrobbleHold _scrobbleHold;
    public ScrobbleHold Build() => _scrobbleHold;

    public ScrobbleHoldBuilder(ScrobbleHold? hold = null)
    {
        if (hold != null)
        {
            _scrobbleHold = hold;
            return;
        }

        _scrobbleHold = new ScrobbleHold();
    }

    public ScrobbleHoldBuilder WithSeriesId(int seriesId)
    {
        _scrobbleHold.SeriesId = seriesId;
        return this;
    }
}
