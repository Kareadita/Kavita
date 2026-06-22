using System.Collections.Generic;
using Kavita.API.Database;
using Kavita.Models.DTOs.KavitaPlus.Scrobble;
using Kavita.Models.Entities.Enums;
using Kavita.Models.Entities.Enums.UserPreferences;
using Kavita.Services.Plus.ScrobbleService;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace Kavita.Services.Tests;

/// <summary>
/// Pure tests for the rule configuration hash - the correctness lynchpin of the dedup ledger. A hash that is
/// unstable or that collides causes silent permanent suppression, so these guard its key properties.
/// </summary>
public class ScrobbleRuleServiceTests
{
    private static ScrobbleRuleService CreateSut()
        => new(Substitute.For<IUnitOfWork>(), NullLogger<ScrobbleRuleService>.Instance);

    private static ReadStatusTransitionRule Rule(bool enabled = true, int days = 30,
        ScrobbleReadStatus status = ScrobbleReadStatus.OnHold, List<PublicationStatus>? excluded = null)
        => new()
        {
            Enabled = enabled,
            Days = days,
            TransitionStatus = status,
            ExcludedPublicationStatus = excluded!,
        };

    [Fact]
    public void ComputeHash_IsDeterministic_AcrossInstances()
    {
        var a = CreateSut().ComputeHash(Rule());
        var b = CreateSut().ComputeHash(Rule());

        Assert.Equal(a, b);
    }

    [Fact]
    public void ComputeHash_ExcludesEnabled()
    {
        var enabled = CreateSut().ComputeHash(Rule(enabled: true));
        var disabled = CreateSut().ComputeHash(Rule(enabled: false));

        Assert.Equal(enabled, disabled);
    }

    [Fact]
    public void ComputeHash_ChangesWhenDaysChange()
    {
        var a = CreateSut().ComputeHash(Rule(days: 30));
        var b = CreateSut().ComputeHash(Rule(days: 14));

        Assert.NotEqual(a, b);
    }

    [Fact]
    public void ComputeHash_ChangesWhenTransitionStatusChanges()
    {
        var a = CreateSut().ComputeHash(Rule(status: ScrobbleReadStatus.OnHold));
        var b = CreateSut().ComputeHash(Rule(status: ScrobbleReadStatus.Dropped));

        Assert.NotEqual(a, b);
    }

    [Fact]
    public void ComputeHash_IsInvariantToExcludedStatusOrder()
    {
        var a = CreateSut().ComputeHash(Rule(excluded: [PublicationStatus.OnGoing, PublicationStatus.Hiatus]));
        var b = CreateSut().ComputeHash(Rule(excluded: [PublicationStatus.Hiatus, PublicationStatus.OnGoing]));

        Assert.Equal(a, b);
    }

    [Fact]
    public void ComputeHash_ChangesWhenExcludedStatusContentChanges()
    {
        var a = CreateSut().ComputeHash(Rule(excluded: [PublicationStatus.OnGoing]));
        var b = CreateSut().ComputeHash(Rule(excluded: [PublicationStatus.OnGoing, PublicationStatus.Cancelled]));

        Assert.NotEqual(a, b);
    }

    [Fact]
    public void ComputeHash_TreatsNullAndEmptyExcludedListAsEqual()
    {
        var withNull = CreateSut().ComputeHash(Rule(excluded: null));
        var withEmpty = CreateSut().ComputeHash(Rule(excluded: []));

        Assert.Equal(withNull, withEmpty);
    }
}
