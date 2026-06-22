using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Kavita.API.Database;
using Kavita.API.Services.Plus;
using Kavita.Common;
using Kavita.Common.Helpers;
using Kavita.Models.DTOs.KavitaPlus.Scrobble;
using Kavita.Models.DTOs.Scrobbling;
using Kavita.Models.Entities;
using Kavita.Models.Entities.Enums;
using Kavita.Models.Entities.Enums.KavitaPlus;
using Kavita.Models.Entities.Scrobble;
using Kavita.Models.Entities.User;
using Microsoft.Extensions.Logging;

namespace Kavita.Services.Plus.ScrobbleService;

public class AniListScrobbleProviderService(ILogger<AniListScrobbleProviderService> logger, IUnitOfWork unitOfWork, IKavitaPlusAuditService auditService)
    : SeriesScrobbleService<AniListScrobbleProviderService>(logger, unitOfWork, auditService)
{
    protected override ScrobbleProvider Provider => ScrobbleProvider.AniList;
    protected override IReadOnlyList<ScrobbleEventType> SupportedEvents =>
    [
        ScrobbleEventType.ChapterRead, ScrobbleEventType.AddWantToRead, ScrobbleEventType.RemoveWantToRead,
        ScrobbleEventType.ScoreUpdated, ScrobbleEventType.Review, ScrobbleEventType.ReadStatusUpdate
    ];
    protected override void SetScrobbleIds(ScrobbleEvent evt, Series series)
    {
        evt.AniListId = series.AniListId;
    }

    // AniList's rate limit is enforced server-wide (~30 requests/min), shared across all users.
    // 30/min == one request every 2s
    public override RateProfile RateProfile => new(
        BaseInterval: TimeSpan.FromSeconds(2),
        Buffer: TimeSpan.FromMilliseconds(300),
        LowRateThreshold: 10,
        RebuildWait: TimeSpan.FromSeconds(60),
        Scope: RateScope.Server);

    public override bool IsTokenValid(string token)
    {
        return JwtHelper.IsTokenValid(token);
    }
}
