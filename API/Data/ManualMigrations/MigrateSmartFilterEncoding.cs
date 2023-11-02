using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using API.DTOs.Filtering.v2;
using API.Helpers;
using Microsoft.Extensions.Logging;

namespace API.Data.ManualMigrations;

/// <summary>
/// v0.7.10.2 introduced a bad encoding, this will migrate those bad smart filters
/// </summary>
public static class MigrateSmartFilterEncoding
{
    public static async Task Migrate(IUnitOfWork unitOfWork, DataContext dataContext, ILogger<Program> logger)
    {
        logger.LogCritical("Running MigrateSmartFilterEncoding migration - Please be patient, this may take some time. This is not an error");

        var statementsRegex = new Regex("stmts=(?<Statements>.*?)&");
        const string valueRegex = @"value=(?<value>\d+)";
        const string fieldRegex = @"field=(?<value>\d+)";
        const string comparisonRegex = @"comparison=(?<value>\d+)";
        var smartFilters = dataContext.AppUserSmartFilter.ToList();
        foreach (var filter in smartFilters)
        {
            if (filter.Filter.Contains(SmartFilterHelper.StatementSeparator)) continue;
            var statements = statementsRegex.Matches(filter.Filter)
                .Select(match => match.Groups["Statements"])
                .FirstOrDefault(group => group.Success && group != Match.Empty)?.Value;
            if (string.IsNullOrEmpty(statements)) continue;


            // We have statements. Let's remove the statements and generate a filter dto
            var noStmt = statementsRegex.Replace(filter.Filter, string.Empty).Replace("stmts=", string.Empty);
            var filterDto = SmartFilterHelper.Decode(noStmt);

            // Now we just parse each individual stmt into the core components and add to statements

            var individualParts = Uri.UnescapeDataString(statements).Split(',').Select(Uri.UnescapeDataString);
            foreach (var part in individualParts)
            {
                filterDto.Statements.Add(new FilterStatementDto()
                {
                    Value = Regex.Match(part, valueRegex).Groups["value"].Value,
                    Field = Enum.Parse<FilterField>(Regex.Match(part, fieldRegex).Groups["value"].Value),
                    Comparison = Enum.Parse<FilterComparison>(Regex.Match(part, comparisonRegex).Groups["value"].Value),
                });
            }

            filter.Filter = SmartFilterHelper.Encode(filterDto);
        }

        if (unitOfWork.HasChanges())
        {
            await unitOfWork.CommitAsync();
        }

        logger.LogCritical("Running MigrateSmartFilterEncoding migration - Completed. This is not an error");
    }
}
