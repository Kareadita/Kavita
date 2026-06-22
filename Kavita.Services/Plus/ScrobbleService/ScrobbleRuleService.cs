using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Kavita.API.Database;
using Kavita.API.Services.Plus;
using Kavita.Models.DTOs.KavitaPlus.Scrobble;
using Kavita.Models.Entities.Enums;
using Kavita.Models.Entities.History;
using Kavita.Models.Entities.Scrobble;
using Microsoft.Extensions.Logging;

namespace Kavita.Services.Plus.ScrobbleService;
#nullable enable

/// <summary>
/// <inheritdoc cref="IScrobbleRuleService"/>
/// </summary>
public class ScrobbleRuleService(IUnitOfWork unitOfWork, ILogger<ScrobbleRuleService> logger) : IScrobbleRuleService
{
    public string ComputeHash(ReadStatusTransitionRule rule)
    {
        // Excludes Enabled by design. Sort the excluded statuses so logically-equal rules hash identically.
        var excluded = (rule.ExcludedPublicationStatus ?? [])
            .Select(s => (int) s)
            .OrderBy(x => x);

        var canonical = $"{rule.Days}|{(int) rule.TransitionStatus}|{string.Join(",", excluded)}";
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(canonical));
        return Convert.ToHexString(bytes);
    }

    public Task ResetReadSeriesAsync(int userId, CancellationToken ct = default)
    {
        return unitOfWork.ScrobbleRepository.PurgeReadSinceDeliveryRuleHistory(userId, ct);
    }

    public async Task<HashSet<(int SeriesId, int? ChapterId)>> GetDeliveredKeysAsync(int userId,
        ScrobbleProvider provider, TransitionRuleKind ruleKind, string currentHash, CancellationToken ct = default)
    {
        var rows = await unitOfWork.ScrobbleRepository.GetRuleHistoryForProviderKind(userId, provider, ruleKind, ct);
        return rows
            .Where(r => r.RuleHash == currentHash)
            .Select(r => (r.SeriesId, r.ChapterId))
            .ToHashSet();
    }

    public async Task RecordDeliveredAsync(ScrobbleEvent evt, CancellationToken ct = default)
    {
        // Only rule-originated events carry a kind + pinned hash
        if (evt.TransitionRuleKind is not { } ruleKind || string.IsNullOrEmpty(evt.RuleHashSnapshot)) return;

        var existing = await unitOfWork.ScrobbleRepository.GetRuleHistory(
            evt.AppUserId, evt.ScrobbleProvider, ruleKind, evt.SeriesId, evt.ChapterId, ct);

        if (existing != null)
        {
            existing.RuleHash = evt.RuleHashSnapshot;
            existing.CreatedUtc = DateTime.UtcNow;
            existing.ScrobbleEventId = evt.Id;
        }
        else
        {
            unitOfWork.ScrobbleRepository.AttachRuleHistory(new ScrobbleRuleHistory
            {
                AppUserId = evt.AppUserId,
                Provider = evt.ScrobbleProvider,
                RuleKind = ruleKind,
                SeriesId = evt.SeriesId,
                ChapterId = evt.ChapterId,
                RuleHash = evt.RuleHashSnapshot,
                CreatedUtc = DateTime.UtcNow,
                ScrobbleEventId = evt.Id,
            });
        }

        logger.LogDebug("Recorded {RuleKind} transition delivery for Series {SeriesId} (Chapter {ChapterId}) by User {UserId}",
            ruleKind, evt.SeriesId, evt.ChapterId, evt.AppUserId);
    }

    public Task PurgeForProviderAsync(int userId, ScrobbleProvider provider, CancellationToken ct = default)
    {
        return unitOfWork.ScrobbleRepository.PurgeRuleHistoryForProvider(userId, provider, ct);
    }

    public async Task PurgeStaleForSettingsAsync(int userId, ScrobbleProvider provider,
        ScrobbleProviderSettingsDto settings, CancellationToken ct = default)
    {
        await PurgeStaleForRuleAsync(userId, provider, TransitionRuleKind.Inactive, settings.InactiveSeriesRule, ct);
        await PurgeStaleForRuleAsync(userId, provider, TransitionRuleKind.Dropped, settings.DroppedSeriesRule, ct);
    }

    private async Task PurgeStaleForRuleAsync(int userId, ScrobbleProvider provider, TransitionRuleKind ruleKind,
        ReadStatusTransitionRule rule, CancellationToken ct)
    {
        var hash = ComputeHash(rule);

        // Remove old historical items if the actual hash changed (enable/disable does not apply)
        await unitOfWork.ScrobbleRepository.PurgeRuleHistoryByHashMismatch(userId, provider, ruleKind, hash, ct);

        // Drop queued events the old config produced so they don't deliver after the change. A disabled
        // rule produces nothing on recompute, so all its queued events are dropped (keepHash = null).
        var enabled = rule is { Enabled: true, Days: > 0 };
        await unitOfWork.ScrobbleRepository.PurgeUnprocessedRuleEvents(userId, provider, ruleKind, enabled ? hash : null, ct);
    }
}
