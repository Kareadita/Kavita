using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using API.DTOs.Filtering;
using API.DTOs.Filtering.v2;

#nullable enable

namespace API.Helpers;

public static class SmartFilterHelper
{
    private const string SortOptionsKey = "sortOptions=";
    private const string NameKey = "name=";
    private const string SortFieldKey = "sortField=";
    private const string IsAscendingKey = "isAscending=";
    private const string StatementsKey = "stmts=";
    private const string LimitToKey = "limitTo=";
    private const string CombinationKey = "combination=";
    private const string StatementComparisonKey = "comparison=";
    private const string StatementFieldKey = "field=";
    private const string StatementValueKey = "value=";
    public const string StatementSeparator = "\ufffd";
    public const string InnerStatementSeparator = "¦";

    public static FilterV2Dto Decode(string? encodedFilter)
    {
        if (string.IsNullOrWhiteSpace(encodedFilter))
        {
            return new FilterV2Dto(); // Create a default filter if the input is empty
        }

        var parts = encodedFilter.Split('&');
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
            else if (part.StartsWith(NameKey))
            {
                filter.Name = HttpUtility.UrlDecode(part.Substring(5));
            }
        }

        return filter;
    }

    public static string Encode(FilterV2Dto? filter)
    {
        if (filter == null)
            return string.Empty;

        var encodedStatements = EncodeFilterStatementDtos(filter.Statements);
        var encodedSortOptions = filter.SortOptions != null
            ? $"{SortOptionsKey}{EncodeSortOptions(filter.SortOptions)}"
            : string.Empty;
        var encodedLimitTo = $"{LimitToKey}{filter.LimitTo}";

        return $"{EncodeName(filter.Name)}{encodedStatements}&{encodedSortOptions}&{encodedLimitTo}&{CombinationKey}{(int) filter.Combination}";
    }

    private static string EncodeName(string? name)
    {
        return string.IsNullOrWhiteSpace(name) ? string.Empty : $"{NameKey}{Uri.EscapeDataString(name)}&";
    }

    private static string EncodeSortOptions(SortOptions sortOptions)
    {
        return Uri.EscapeDataString($"{SortFieldKey}{(int) sortOptions.SortField}{InnerStatementSeparator}{IsAscendingKey}{sortOptions.IsAscending}");
    }

    private static string EncodeFilterStatementDtos(ICollection<FilterStatementDto>? statements)
    {
        if (statements == null || statements.Count == 0)
            return string.Empty;

        var encodedStatements = StatementsKey + Uri.EscapeDataString(string.Join(StatementSeparator, statements.Select(EncodeFilterStatementDto)));
        return encodedStatements;
    }

    private static string EncodeFilterStatementDto(FilterStatementDto statement)
    {

        var encodedComparison = $"{StatementComparisonKey}{(int) statement.Comparison}";
        var encodedField = $"{StatementFieldKey}{(int) statement.Field}";
        var encodedValue = $"{StatementValueKey}{Uri.EscapeDataString(statement.Value)}";

        return Uri.EscapeDataString($"{encodedComparison}{InnerStatementSeparator}{encodedField}{InnerStatementSeparator}{encodedValue}");
    }

    private static List<FilterStatementDto> DecodeFilterStatementDtos(string encodedStatements)
    {
        var statementStrings = Uri.UnescapeDataString(encodedStatements).Split(StatementSeparator);

        var statements = new List<FilterStatementDto>();

        foreach (var statementString in statementStrings)
        {
            var parts = Uri.UnescapeDataString(statementString).Split(InnerStatementSeparator);
            if (parts.Length < 3)
                continue;

            statements.Add(new FilterStatementDto
            {
                Comparison = Enum.Parse<FilterComparison>(parts[0].Split("=")[1]),
                Field = Enum.Parse<FilterField>(parts[1].Split("=")[1]),
                Value = Uri.UnescapeDataString(parts[2].Split("=")[1])
            });
        }

        return statements;
    }

    private static SortOptions DecodeSortOptions(string encodedSortOptions)
    {
        var parts = Uri.UnescapeDataString(encodedSortOptions).Split(InnerStatementSeparator);

        var sortFieldPart = Array.Find(parts, part => part.StartsWith(SortFieldKey));
        var isAscendingPart = Array.Find(parts, part => part.StartsWith(IsAscendingKey));

        var isAscending = isAscendingPart?.Trim().Replace(IsAscendingKey, string.Empty).Equals("true", StringComparison.OrdinalIgnoreCase) ?? false;
        if (sortFieldPart == null)
        {
            return new SortOptions();
        }

        var sortField = Enum.Parse<SortField>(sortFieldPart.Split("=")[1]);

        return new SortOptions
        {
            SortField = sortField,
            IsAscending = isAscending
        };
    }
}
