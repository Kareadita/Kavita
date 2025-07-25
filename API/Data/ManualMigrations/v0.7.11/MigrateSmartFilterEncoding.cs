using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using API.DTOs.Filtering.v2;
using API.Entities;
using API.Entities.History;
using API.Helpers;
using Kavita.Common.EnvironmentInfo;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace API.Data.ManualMigrations;

/// <summary>
/// v0.7.10.2 introduced a bad encoding, this will migrate those bad smart filters
/// </summary>
public static class MigrateSmartFilterEncoding
{
    private static readonly Regex StatementsRegex = new Regex("stmts=(?<Statements>.*?)&");
    private const string ValueRegex = @"value=(?<value>\d+)";
    private const string FieldRegex = @"field=(?<value>\d+)";
    private const string ComparisonRegex = @"comparison=(?<value>\d+)";
    private const string SortOptionsRegex = @"sortField=(.*?),isAscending=(.*?)";

    public static async Task Migrate(IUnitOfWork unitOfWork, DataContext dataContext, ILogger<Program> logger)
    {
        if (await dataContext.ManualMigrationHistory.AnyAsync(m => m.Name == "MigrateSmartFilterEncoding"))
        {
            return;
        }

        logger.LogCritical("Running MigrateSmartFilterEncoding migration - Please be patient, this may take some time. This is not an error");

        var smartFilters = await dataContext.AppUserSmartFilter.ToListAsync();
        foreach (var filter in smartFilters)
        {
            if (!ShouldMigrateFilter(filter.Filter)) continue;
            var decode = EncodeFix(filter.Filter);
            if (string.IsNullOrEmpty(decode)) continue;
            filter.Filter = decode;
        }

        if (unitOfWork.HasChanges())
        {
            await unitOfWork.CommitAsync();
        }

        dataContext.ManualMigrationHistory.Add(new ManualMigrationHistory()
        {
            Name = "MigrateSmartFilterEncoding",
            ProductVersion = BuildInfo.Version.ToString(),
            RanAt = DateTime.UtcNow
        });
        await dataContext.SaveChangesAsync();

        logger.LogCritical("Running MigrateSmartFilterEncoding migration - Completed. This is not an error");
    }

    public static bool ShouldMigrateFilter(string filter)
    {
        return !string.IsNullOrEmpty(filter) && !(filter.Contains(SmartFilterHelper.StatementSeparator) || Uri.UnescapeDataString(filter).Contains(SmartFilterHelper.StatementSeparator));
    }

    public static string EncodeFix(string encodedFilter)
    {
        var statements = StatementsRegex.Matches(encodedFilter)
            .Select(match => match.Groups["Statements"])
            .FirstOrDefault(group => group.Success && group != Match.Empty)?.Value;
        if (string.IsNullOrEmpty(statements)) return encodedFilter;


        // We have statements. Let's remove the statements and generate a filter dto
        var noStmt = StatementsRegex.Replace(encodedFilter, string.Empty).Replace("stmts=", string.Empty);

        // Pre-v0.7.10 filters could be extra escaped
        if (!noStmt.Contains("sortField="))
        {
            noStmt = Uri.UnescapeDataString(noStmt);
        }

        // We need to replace sort options portion with a properly encoded
        noStmt = Regex.Replace(noStmt, SortOptionsRegex, match =>
        {
            var sortFieldValue = match.Groups[1].Value;
            var isAscendingValue = match.Groups[2].Value;

            return $"sortField={sortFieldValue}{SmartFilterHelper.InnerStatementSeparator}isAscending={isAscendingValue}";
        });

        //name=Zero&sortOptions=sortField=2&isAscending=False&limitTo=0&combination=1
        var filterDto = SmartFilterHelper.Decode(noStmt);

        // Now we just parse each individual stmt into the core components and add to statements

        var individualParts = Uri.UnescapeDataString(statements).Split(',').Select(Uri.UnescapeDataString);
        foreach (var part in individualParts)
        {
            filterDto.Statements.Add(new FilterStatementDto()
            {
                Value = Regex.Match(part, ValueRegex).Groups["value"].Value,
                Field = Enum.Parse<FilterField>(Regex.Match(part, FieldRegex).Groups["value"].Value),
                Comparison = Enum.Parse<FilterComparison>(Regex.Match(part, ComparisonRegex).Groups["value"].Value),
            });
        }
        return SmartFilterHelper.Encode(filterDto);
    }
}
