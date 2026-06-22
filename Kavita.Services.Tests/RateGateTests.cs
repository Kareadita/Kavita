using System;
using Kavita.API.Services.Plus;
using Kavita.Services.Plus;
using Xunit;

namespace Kavita.Services.Tests;

/// <summary>
/// Pure (no DB) tests for the per-provider throttle gate that drives scrobble pacing and backoff.
/// </summary>
public class RateGateTests
{
    private static readonly TimeSpan BaseInterval = TimeSpan.FromMilliseconds(700);
    private static readonly TimeSpan Buffer = TimeSpan.FromMilliseconds(300);
    private const int Threshold = 10;
    private static readonly TimeSpan RebuildWait = TimeSpan.FromSeconds(60);

    private static ScrobbleSyncContext.RateGate CreateGate() => new(new RateProfile(
        BaseInterval, Buffer, Threshold, RebuildWait, RateScope.Server));

    [Fact]
    public void FreshGate_HasNoWait_AndNoBudgetUntilSeeded()
    {
        var gate = CreateGate();

        Assert.Equal(TimeSpan.Zero, gate.GetWaitTime());
        Assert.False(gate.HasRateLeft());
    }

    [Fact]
    public void Seed_SetsBudget_WithoutSchedulingAWait()
    {
        var gate = CreateGate();

        gate.Seed(50);

        Assert.True(gate.HasRateLeft());
        Assert.Equal(TimeSpan.Zero, gate.GetWaitTime());
    }

    [Fact]
    public void Seed_WithZeroBudget_HasNoRateLeft()
    {
        var gate = CreateGate();

        gate.Seed(0);

        Assert.False(gate.HasRateLeft());
    }

    [Fact]
    public void RecordResult_AboveThreshold_SchedulesBaseRate()
    {
        var gate = CreateGate();
        gate.Seed(100);

        gate.RecordResult(Threshold + 1);

        var wait = gate.GetWaitTime();
        Assert.True(gate.HasRateLeft());
        // Should be ~BaseRate (interval + buffer), allowing for elapsed time since RecordResult
        Assert.True(wait > TimeSpan.Zero, $"Expected a positive wait, got {wait}");
        Assert.True(wait <= BaseInterval + Buffer, $"Expected wait <= BaseRate, got {wait}");
        Assert.True(wait < RebuildWait, $"Expected base-rate wait, not a rebuild backoff, got {wait}");
    }

    [Fact]
    public void RecordResult_AtThreshold_SchedulesRebuildBackoff()
    {
        var gate = CreateGate();
        gate.Seed(100);

        gate.RecordResult(Threshold);

        var wait = gate.GetWaitTime();
        // Should be ~RebuildWait, clearly larger than the base rate
        Assert.True(wait > BaseInterval + Buffer, $"Expected a rebuild backoff, got {wait}");
        Assert.True(wait <= RebuildWait, $"Expected wait <= RebuildWait, got {wait}");
    }

    [Fact]
    public void RecordResult_Zero_ExhaustsTheGate()
    {
        var gate = CreateGate();
        gate.Seed(100);

        gate.RecordResult(0);

        Assert.False(gate.HasRateLeft());
    }
}
