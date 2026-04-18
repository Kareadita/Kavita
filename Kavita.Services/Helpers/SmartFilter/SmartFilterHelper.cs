using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using Kavita.Models.DTOs.Filtering.v2;
using Kavita.Models.DTOs.Filtering.v2.FilterFields;
using Kavita.Models.DTOs.Filtering.v2.Requests;
using Kavita.Models.DTOs.Filtering.v2.SortFields;
using Kavita.Models.DTOs.Filtering.v2.SortOptions;

namespace Kavita.Services.Helpers.SmartFilter;

public static class SmartFilterHelper
{
    private const string SortOptionsKey = "sortOptions=";
    private const string NameKey = "name=";
    private const string EntityTypeKey = "entityType=";
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


    public static IFilterDto Decode(string? encodedFilter)
    {
        var entityType = PeekEntityType(encodedFilter);

        return entityType switch
        {
            FilterEntityType.Series =>
                DecodeFilter<SeriesFilterV2Dto, SeriesFilterStatementDto, SeriesFilterField, SeriesSortOptionDto, SeriesSortField>(encodedFilter),
            FilterEntityType.ReadingList =>
                DecodeFilter<ReadingListFilterDto, ReadingListFilterStatementDto, ReadingListFilterField, ReadingListSortOptionDto, ReadingListSortField>(encodedFilter),
            FilterEntityType.Person =>
                DecodeFilter<PersonFilterDto, PersonFilterStatementDto, PersonFilterField, PersonSortOptionDto, PersonSortField>(encodedFilter),
            FilterEntityType.Annotation => DecodeFilter<AnnotationFilterDto, AnnotationFilterStatementDto, AnnotationFilterField, AnnotationSortOptionDto, AnnotationSortField>(encodedFilter),
            _ => throw new ArgumentOutOfRangeException(nameof(entityType), entityType, null)
        };
    }

    private static TFilter DecodeFilter<TFilter, TStatement, TField, TSortOption, TSortField>(string? encodedFilter)
        where TFilter : IFilterDto<TStatement, TSortOption>, new()
        where TStatement : IFilterStatement<TField>, new()
        where TField : struct, Enum
        where TSortOption : class, ISortOptionDto<TSortField>, new()
        where TSortField : struct, Enum
    {
        if (string.IsNullOrWhiteSpace(encodedFilter))
            return new TFilter();

        var parts = encodedFilter.Split('&');
        var filter = new TFilter();

        foreach (var part in parts)
        {
            if (part.StartsWith(SortOptionsKey))
            {
                filter.SortOptions = DecodeSortOptions<TSortField, TSortOption>(part.Substring(SortOptionsKey.Length));
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
                filter.Statements = DecodeFilterStatementDtos<TField, TStatement>(part.Substring(StatementsKey.Length));
            }
            else if (part.StartsWith(NameKey))
            {
                filter.Name = HttpUtility.UrlDecode(part.Substring(5));
            }
        }

        return filter;
    }


    /// <summary>
    /// Old code that only handles Series. This is only for <c>MigrateSmartFilterEncoding</c>
    /// </summary>
    /// <param name="encodedFilter"></param>
    /// <returns></returns>
    public static SeriesFilterV2Dto DecodeLegacy(string? encodedFilter)
    {
        if (string.IsNullOrWhiteSpace(encodedFilter))
        {
            return new SeriesFilterV2Dto(); // Create a default filter if the input is empty
        }

        var parts = encodedFilter.Split('&');
        var filter = new SeriesFilterV2Dto();

        foreach (var part in parts)
        {
            if (part.StartsWith(SortOptionsKey))
            {
                filter.SortOptions = DecodeSortOptions<SeriesSortField, SeriesSortOptionDto>(part.Substring(SortOptionsKey.Length));
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
                filter.Statements = DecodeFilterStatementDtos<SeriesFilterField, SeriesFilterStatementDto>(part.Substring(StatementsKey.Length));
            }
            else if (part.StartsWith(NameKey))
            {
                filter.Name = HttpUtility.UrlDecode(part.Substring(5));
            }
        }

        return filter;
    }


    private static string EncodeFilter<TStatement, TField, TSortOption, TSortField>(
        IFilterDto<TStatement, TSortOption>? filter)
        where TStatement : IFilterStatement<TField>
        where TField : struct, Enum
        where TSortOption : class, ISortOptionDto<TSortField>, new()
        where TSortField : struct, Enum
    {
        if (filter == null) return string.Empty;

        var encodedStatements = EncodeFilterStatementDtos<TField, TStatement>(filter.Statements);
        var encodedSortOptions = filter.SortOptions != null
            ? $"{SortOptionsKey}{EncodeSortOptions(filter.SortOptions)}"
            : string.Empty;
        var encodedLimitTo = $"{LimitToKey}{filter.LimitTo}";

        return $"{EncodeName(filter.Name)}{EncodeEntityType(filter.EntityType)}{encodedStatements}&{encodedSortOptions}&{encodedLimitTo}&{CombinationKey}{(int) filter.Combination}";
    }

    public static string Encode(IFilterDto? filter)
    {
        if (filter == null) return string.Empty;
        return filter.EntityType switch
        {
            FilterEntityType.Series => Encode((SeriesFilterV2Dto?) filter),
            FilterEntityType.ReadingList => Encode((ReadingListFilterDto?) filter),
            FilterEntityType.Person => Encode((PersonFilterDto?) filter),
            FilterEntityType.Annotation => Encode((AnnotationFilterDto?) filter),
            _ => string.Empty
        };
    }
    public static string Encode(SeriesFilterV2Dto? filter)
    {
        return EncodeFilter<SeriesFilterStatementDto, SeriesFilterField, SeriesSortOptionDto, SeriesSortField>(filter);
    }
    public static string Encode(ReadingListFilterDto? filter)
    {
        return EncodeFilter<ReadingListFilterStatementDto, ReadingListFilterField, ReadingListSortOptionDto, ReadingListSortField>(filter);
    }
    public static string Encode(PersonFilterDto? filter)
    {
        return EncodeFilter<PersonFilterStatementDto, PersonFilterField, PersonSortOptionDto, PersonSortField>(filter);
    }
    public static string Encode(AnnotationFilterDto? filter)
    {
        return EncodeFilter<AnnotationFilterStatementDto, AnnotationFilterField, AnnotationSortOptionDto, AnnotationSortField>(filter);
    }


    /// <summary>
    /// Checks the entity type from the encoded string. If not valid or missing, defaults to Series
    /// </summary>
    /// <param name="encodedFilter"></param>
    /// <returns></returns>
    private static FilterEntityType PeekEntityType(string? encodedFilter)
    {
        if (string.IsNullOrEmpty(encodedFilter)) return FilterEntityType.Series;

        var parts = encodedFilter.Split("entityType=");
        if (parts.Length == 1)
        {
            return FilterEntityType.Series; // Assume this is a Series filter by default
        }

        return Enum.TryParse<FilterEntityType>(parts[1].Split('&')[0], out var enumType) ? enumType : FilterEntityType.Series;
    }

    private static string EncodeName(string? name)
    {
        return string.IsNullOrWhiteSpace(name) ? string.Empty : $"{NameKey}{Uri.EscapeDataString(name)}&";
    }

    private static string EncodeEntityType(FilterEntityType entityType)
    {
        return $"{EntityTypeKey}{entityType.ToString()}&";
    }

    private static string EncodeSortOptions<TField>(ISortOptionDto<TField> sortOptionDto)
    where TField : struct, Enum
    {
        return Uri.EscapeDataString($"{SortFieldKey}{Convert.ToInt32(sortOptionDto.SortField)}{InnerStatementSeparator}{IsAscendingKey}{sortOptionDto.IsAscending}");
    }

    private static string EncodeFilterStatementDtos<TField, TStatement>(ICollection<TStatement>? statements)
    where TField : struct, Enum
    where TStatement : IFilterStatement<TField>
    {
        if (statements == null || statements.Count == 0)
            return string.Empty;

        var encodedStatements = StatementsKey + Uri.EscapeDataString(string.Join(StatementSeparator, statements.Select(EncodeFilterStatementDto<TField, TStatement>)));
        return encodedStatements;
    }

    private static string EncodeFilterStatementDto<TField, TStatement>(TStatement statement)
        where TField : struct, Enum
        where TStatement : IFilterStatement<TField>
    {

        var encodedComparison = $"{StatementComparisonKey}{(int) statement.Comparison}";
        var encodedField = $"{StatementFieldKey}{Convert.ToInt32(statement.Field)}";
        var encodedValue = $"{StatementValueKey}{Uri.EscapeDataString(statement.Value)}";

        return Uri.EscapeDataString($"{encodedComparison}{InnerStatementSeparator}{encodedField}{InnerStatementSeparator}{encodedValue}");
    }

    private static List<TStatement> DecodeFilterStatementDtos<TField, TStatement>(string encodedStatements)
        where TField : struct, Enum
        where TStatement : IFilterStatement<TField>, new()
    {
        var statementStrings = Uri.UnescapeDataString(encodedStatements).Split(StatementSeparator);

        var statements = new List<TStatement>();

        foreach (var statementString in statementStrings)
        {
            var parts = Uri.UnescapeDataString(statementString).Split(InnerStatementSeparator);
            if (parts.Length < 3)
                continue;

            statements.Add(new TStatement
            {
                Comparison = Enum.Parse<FilterComparison>(parts[0].Split("=")[1]),
                Field = Enum.Parse<TField>(parts[1].Split("=")[1]),
                Value = Uri.UnescapeDataString(parts[2].Split("=")[1])
            });
        }

        return statements;
    }

    private static TR DecodeSortOptions<T, TR>(string encodedSortOptions)
        where T : struct, Enum
        where TR : ISortOptionDto<T>, new()
    {
        var parts = Uri.UnescapeDataString(encodedSortOptions).Split(InnerStatementSeparator);

        var sortFieldPart = Array.Find(parts, part => part.StartsWith(SortFieldKey));
        var isAscendingPart = Array.Find(parts, part => part.StartsWith(IsAscendingKey));

        var isAscending = isAscendingPart?.Trim().Replace(IsAscendingKey, string.Empty).Equals("true", StringComparison.OrdinalIgnoreCase) ?? false;
        if (sortFieldPart == null)
        {
            return new TR();
        }

        var sortField = Enum.Parse<T>(sortFieldPart.Split("=")[1]);

        return new TR
        {
            SortField = sortField,
            IsAscending = isAscending
        };
    }
}
