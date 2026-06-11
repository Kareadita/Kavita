using System;
using System.Collections.Generic;
using Kavita.API.Database;
using Kavita.API.Services.Plus;
using Kavita.Models.DTOs.Scrobbling;
using Kavita.Models.Entities;
using Kavita.Models.Entities.Enums;
using Kavita.Models.Entities.Enums.KavitaPlus;
using Kavita.Models.Entities.Scrobble;
using Microsoft.Extensions.Logging;

namespace Kavita.Services.Plus.ScrobbleService;

public class MyAnimeListScrobbleProviderService(ILogger<MyAnimeListScrobbleProviderService> logger, IUnitOfWork unitOfWork, IKavitaPlusAuditService auditService)
    : SeriesScrobbleService<MyAnimeListScrobbleProviderService>(logger, unitOfWork, auditService)
{
    protected override ScrobbleProvider Provider => ScrobbleProvider.Mal;
    protected override IReadOnlyList<ScrobbleEventType> SupportedEvents =>
    [
        // Not Supported
    ];

    protected override void SetScrobbleIds(ScrobbleEvent evt, Series series)
    {
        evt.MalId = series.MalId;
    }

    // This is just a baseline, we don't support MAL yet
    public override RateProfile RateProfile => new(
        BaseInterval: TimeSpan.FromSeconds(1),
        Buffer: TimeSpan.FromMilliseconds(500),
        LowRateThreshold: 5,
        RebuildWait: TimeSpan.FromSeconds(60),
        Scope: RateScope.Server);

    public override bool IsTokenValid(string token)
    {
        // I don't actually know what Mal uses, but it's always valid whatever
        return true;
    }
}
