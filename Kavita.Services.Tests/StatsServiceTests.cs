using Kavita.Models.DTOs.KavitaPlus.License;
using Kavita.Models.DTOs.Stats.V3;

namespace Kavita.Services.Tests;

public class StatsServiceTests
{
    [Theory]
    [InlineData(KavitaPlusSubscriptionState.Active, KavitaPlusState.Active)]
    [InlineData(KavitaPlusSubscriptionState.Cancelled, KavitaPlusState.Expired)]
    [InlineData(KavitaPlusSubscriptionState.Paused, KavitaPlusState.Paused)]
    [InlineData(KavitaPlusSubscriptionState.Cancelling, KavitaPlusState.Cancelling)]
    [InlineData(KavitaPlusSubscriptionState.Expired, KavitaPlusState.Expired)]
    public void ToStatsKavitaPlusState_MapsByName(KavitaPlusSubscriptionState state, KavitaPlusState expected)
    {
        Assert.Equal(expected, StatsService.ToStatsKavitaPlusState(state));
    }
}
