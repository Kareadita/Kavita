using System.Threading.Tasks;
using Kavita.Database;
using Kavita.Models.DTOs.Filtering.v2;
using Kavita.Models.DTOs.Filtering.v2.Requests;
using Kavita.Services.Helpers.SmartFilter;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Kavita.Server.ManualMigrations.v0._9._1;

/// <summary>
/// The "Read Last" Series smart filter compares on days-since-last-read, which maps to a cutoff date of
/// <c>Now - N days</c>. A larger day count therefore corresponds to an *earlier* last-read date, so the numeric
/// comparisons had been applied inverted (see #4716). The code fix flips GreaterThan/LessThan (and their -OrEqual
/// variants) for this field. Existing saved smart filters were built against the old (inverted) behaviour, so this
/// migration flips those same numeric operators on every stored "Read Last" statement. The net effect is that a
/// saved filter keeps returning exactly the same series it did before the fix.
/// </summary>
/// <remarks>
/// Only GreaterThan &lt;-&gt; LessThan and GreaterThanEqual &lt;-&gt; LessThanEqual are flipped. Equal/NotEqual are
/// symmetric, and IsAfter/IsBefore keep raw-date semantics in both the old and new code, so none of those change.
/// Only <see cref="SeriesFilterField.ReadLast"/> statements are touched; every other field and filter is left as-is.
/// </remarks>
public class ManualMigrateReadLastSmartFilterComparison : ManualMigration
{
    protected override string MigrationName => nameof(ManualMigrateReadLastSmartFilterComparison);

    protected override async Task ExecuteAsync(DataContext context, ILogger<Program> logger)
    {
        var filters = await context.AppUserSmartFilter.ToListAsync();
        if (filters.Count == 0) return;

        var updated = 0;
        foreach (var filter in filters)
        {
            var migrated = FlipReadLastComparisons(filter.Filter);
            if (migrated == filter.Filter) continue;

            filter.Filter = migrated;
            updated++;
        }

        if (updated > 0)
        {
            await context.SaveChangesAsync();
        }

        logger.LogInformation("[ManualMigrateReadLastSmartFilterComparison] Corrected Read Last comparison(s) in {Count} smart filter(s)", updated);
    }

    /// <summary>
    /// Decodes an encoded smart filter, inverts the numeric comparison on any "Read Last" statement, and re-encodes.
    /// Returns the original string unchanged when the filter is not a Series filter or has no numeric Read Last
    /// statement to flip. Exposed for unit testing.
    /// </summary>
    public static string FlipReadLastComparisons(string encodedFilter)
    {
        if (SmartFilterHelper.Decode(encodedFilter) is not SeriesFilterV2Dto dto) return encodedFilter;
        if (!FlipReadLastStatements(dto)) return encodedFilter;

        return SmartFilterHelper.Encode(dto);
    }

    /// <summary>
    /// Flips the numeric comparison operator on every <see cref="SeriesFilterField.ReadLast"/> statement in place.
    /// Returns <c>true</c> when at least one statement was changed.
    /// </summary>
    public static bool FlipReadLastStatements(SeriesFilterV2Dto dto)
    {
        var changed = false;
        foreach (var statement in dto.Statements)
        {
            if (statement.Field != SeriesFilterField.ReadLast) continue;

            var flipped = FlipComparison(statement.Comparison);
            if (flipped == statement.Comparison) continue;

            statement.Comparison = flipped;
            changed = true;
        }

        return changed;
    }

    /// <summary>
    /// Inverts the numeric comparison operators. All non-numeric comparisons (Equal, NotEqual, IsAfter, IsBefore, ...)
    /// are returned unchanged because their semantics did not change with the #4716 fix.
    /// </summary>
    private static FilterComparison FlipComparison(FilterComparison comparison) => comparison switch
    {
        FilterComparison.GreaterThan => FilterComparison.LessThan,
        FilterComparison.LessThan => FilterComparison.GreaterThan,
        FilterComparison.GreaterThanEqual => FilterComparison.LessThanEqual,
        FilterComparison.LessThanEqual => FilterComparison.GreaterThanEqual,
        _ => comparison
    };
}
