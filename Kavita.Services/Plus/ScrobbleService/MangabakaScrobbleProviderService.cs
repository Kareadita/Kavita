using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Kavita.API.Database;
using Kavita.API.Services.Plus;
using Kavita.Common;
using Kavita.Models.DTOs.KavitaPlus.Scrobble;
using Kavita.Models.DTOs.Scrobbling;
using Kavita.Models.Entities;
using Kavita.Models.Entities.Enums;
using Kavita.Models.Entities.Enums.KavitaPlus;
using Kavita.Models.Entities.Scrobble;
using Kavita.Models.Entities.User;
using Microsoft.Extensions.Logging;

namespace Kavita.Services.Plus.ScrobbleService;

public class MangabakaScrobbleProviderService(ILogger<MangabakaScrobbleProviderService> logger, IUnitOfWork unitOfWork, IKavitaPlusAuditService auditService)
    : SeriesScrobbleService<MangabakaScrobbleProviderService>(logger, unitOfWork, auditService)
{
    protected override ScrobbleProvider Provider => ScrobbleProvider.MangaBaka;
    protected override IReadOnlyList<ScrobbleEventType> SupportedEvents =>
    [
        ScrobbleEventType.ChapterRead, ScrobbleEventType.AddWantToRead, ScrobbleEventType.RemoveWantToRead,
        ScrobbleEventType.ScoreUpdated, ScrobbleEventType.ReadStatusUpdate
    ];
    protected override void SetScrobbleIds(ScrobbleEvent evt, Series series)
    {
        evt.MangabakaId = series.MangaBakaId;
    }

    // MangaBaka is technically unlimited and server-wide (API keys), but we still pace it to be polite (~80/min)
    public override RateProfile RateProfile => new(
        BaseInterval: TimeSpan.FromMilliseconds(500),
        Buffer: TimeSpan.FromMilliseconds(250),
        LowRateThreshold: 5,
        RebuildWait: TimeSpan.FromSeconds(60),
        Scope: RateScope.Server);

    public override bool IsTokenValid(string token)
    {
        // We're using ApiKeys, always valid
        return true;
    }
}
