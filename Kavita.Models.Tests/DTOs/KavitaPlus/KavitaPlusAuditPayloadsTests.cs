using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Kavita.Models.DTOs.KavitaPlus;
using Kavita.Models.DTOs.KavitaPlus.Audit;
using Kavita.Models.Entities.Enums.Audit;
using Xunit;

namespace Kavita.Models.Tests.DTOs.KavitaPlus;

public class KavitaPlusAuditPayloadsTests
{
    [Fact]
    public void EveryAuditEventTypeHasPayloadTypes()
    {
        var unmapped = Enum.GetValues<KavitaPlusEventType>()
            .Where(type => KavitaPlusAuditPayloads.TypesFor(type) == null)
            .ToList();

        Assert.True(unmapped.Count == 0,
            $"Add these to KavitaPlusAuditPayloads.TypesFor, with the payload types their LogAsync calls write: {string.Join(", ", unmapped)}");
    }

    [Fact]
    public void EveryAuditParamsDtoIsMapped()
    {
        var reachable = new KavitaPlusAuditPayloads().AllTypes.SelectMany(Reachable).ToHashSet();
        var missing = typeof(AuditLogScrobbleParamsDto).Assembly.GetTypes()
            .Where(t => t.Name.StartsWith("AuditLog") && t.Name.EndsWith("ParamsDto") && !reachable.Contains(t))
            .Select(t => t.Name)
            .ToList();

        Assert.True(missing.Count == 0,
            $"Not written by any event in KavitaPlusAuditPayloads, so the debug export can't scrub them: {string.Join(", ", missing)}");
    }

    [Fact]
    public void SelectorReadsTheStoredEventType()
    {
        var types = new KavitaPlusAuditPayloads().TypesFor([((int) KavitaPlusEventType.SyncFailed).ToString()]);

        Assert.Contains(typeof(AuditLogCollectionFailedParamsDto), types);
        Assert.Contains(typeof(AuditLogWantToReadSyncParamsDto), types);
    }

    /// <summary>
    /// The type and the payload types nested in its properties, like <see cref="AuditLogMatchExternalIdsParamsDto"/> inside a match
    /// </summary>
    private static IEnumerable<Type> Reachable(Type type)
    {
        yield return type;
        foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (property.PropertyType.Name.StartsWith("AuditLog"))
            {
                foreach (var nested in Reachable(property.PropertyType)) yield return nested;
            }
        }
    }
}
