using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using API.DTOs.Filtering;
using API.DTOs.Filtering.v2;

namespace API.Helpers;

public static class SmartFilterHelper
{
    private const string SortOptionsKey = "sortOptions=";
    private const string StatementsKey = "stmts=";
    private const string LimitToKey = "limitTo=";
    private const string CombinationKey = "combination=";

    public static FilterV2Dto Decode(string? encodedFilter)
    {
        if (string.IsNullOrWhiteSpace(encodedFilter))
        {
            return new FilterV2Dto(); // Create a default filter if the input is empty
        }

        string[] parts = encodedFilter.Split('&');
        var filter = new FilterV2Dto();

        foreach (var part in parts)
        {
            if (part.StartsWith(SortOptionsKey))
            {
                filter.SortOptions = DecodeSortOptions(part.Substring(SortOptionsKey.Length));
            }
            else if (part.StartsWith(LimitToKey))
            {
                filter.LimitTo = int.Parse(part.Substring(LimitToKey.Length));
            }
            else if (part.StartsWith(CombinationKey))
            {
                filter.Combination = Enum.Parse<FilterCombination>(part.Split("=")[1]);
            }
            else if (part.StartsWith(StatementsKey))
            {
                filter.Statements = DecodeFilterStatementDtos(part.Substring(StatementsKey.Length));
            }
            else if (part.StartsWith("name="))
            {
                filter.Name = HttpUtility.UrlDecode(part.Substring(5));
            }
        }

        return filter;
    }

    public static string Encode(FilterV2Dto filter)
    {
        if (filter == null)
            return string.Empty;

        var encodedStatements = EncodeFilterStatementDtos(filter.Statements);
        var encodedSortOptions = filter.SortOptions != null
            ? $"{SortOptionsKey}{EncodeSortOptions(filter.SortOptions)}"
            : "";
        var encodedLimitTo = $"{LimitToKey}{filter.LimitTo}";

        return $"{EncodeName(filter.Name)}{encodedStatements}&{encodedSortOptions}&{encodedLimitTo}&{CombinationKey}{(int) filter.Combination}";
    }

    private static string EncodeName(string name)
    {
        return string.IsNullOrWhiteSpace(name) ? string.Empty : $"name={HttpUtility.UrlEncode(name)}&";
    }

    private static string EncodeSortOptions(SortOptions sortOptions)
    {
        return Uri.EscapeDataString($"sortField={(int) sortOptions.SortField}&isAscending={sortOptions.IsAscending}");
    }

    private static string EncodeFilterStatementDtos(ICollection<FilterStatementDto> statements)
    {
        if (statements == null || statements.Count == 0)
            return string.Empty;

        var encodedStatements = StatementsKey + Uri.EscapeDataString(string.Join(",", statements.Select(EncodeFilterStatementDto)));
        return encodedStatements;
    }

    private static string EncodeFilterStatementDto(FilterStatementDto statement)
    {
        var encodedComparison = $"comparison={(int) statement.Comparison}";
        var encodedField = $"field={(int) statement.Field}";
        var encodedValue = $"value={Uri.EscapeDataString(statement.Value)}";

        return Uri.EscapeDataString($"{encodedComparison},{encodedField},{encodedValue}");
    }

    private static List<FilterStatementDto> DecodeFilterStatementDtos(string encodedStatements)
    {
        encodedStatements = HttpUtility.UrlDecode(encodedStatements);
        string[] statementStrings = encodedStatements.Split(',');

        var statements = new List<FilterStatementDto>();

        foreach (var statementString in statementStrings)
        {
            var parts = statementString.Split('&');
            if (parts.Length < 3)
                continue;

            statements.Add(new FilterStatementDto
            {
                Comparison = Enum.Parse<FilterComparison>(parts[0].Split("=")[1]),
                Field = Enum.Parse<FilterField>(parts[1].Split("=")[1]),
                Value = HttpUtility.UrlDecode(parts[2].Split("=")[1])
            });
        }

        return statements;
    }

    private static SortOptions DecodeSortOptions(string encodedSortOptions)
    {
        string[] parts = encodedSortOptions.Split(',');
        var sortFieldPart = parts.FirstOrDefault(part => part.StartsWith("sortField="));
        var isAscendingPart = parts.FirstOrDefault(part => part.StartsWith("isAscending="));

        var isAscending = isAscendingPart?.Substring(11).Equals("true", StringComparison.OrdinalIgnoreCase) ?? false;
        if (sortFieldPart != null)
        {
            var sortField = Enum.Parse<SortField>(sortFieldPart.Split("=")[1]);

            return new SortOptions
            {
                SortField = sortField,
                IsAscending = isAscending
            };
        }

        return null;
    }
}
