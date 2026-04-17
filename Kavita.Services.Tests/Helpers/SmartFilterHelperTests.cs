using Kavita.Models.DTOs.Filtering;
using Kavita.Models.DTOs.Filtering.v2;
using Kavita.Models.DTOs.Filtering.v2.FilterFields;
using Kavita.Models.DTOs.Filtering.v2.Requests;
using Kavita.Models.DTOs.Filtering.v2.SortFields;
using Kavita.Models.DTOs.Filtering.v2.SortOptions;
using Kavita.Models.Entities.Enums;
using Kavita.Services.Helpers.SmartFilter;

namespace Kavita.Services.Tests.Helpers;

public class SmartFilterHelperTests
{

    [Fact]
    public void Test_Decode()
    {
        const string sep = SmartFilterHelper.StatementSeparator;
        const string encoded = "name=Test&stmts=" +
                               "comparison%253D0%25C2%25A6field%253D18%25C2%25A6value%253D95" + sep +
                               "comparison%253D0%25C2%25A6field%253D4%25C2%25A6value%253D0" + sep +
                               "comparison%253D7%25C2%25A6field%253D1%25C2%25A6value%253Da" +
                               "&sortOptions=sortField%3D2\u00A6isAscending%3DFalse&limitTo=10&combination=1";

        var filter = (SeriesFilterV2Dto) SmartFilterHelper.Decode(encoded);

        Assert.Equal(10, filter.LimitTo);
        Assert.NotNull(filter.SortOptions);
        Assert.Equal(SeriesSortField.CreatedDate, filter.SortOptions.SortField);
        Assert.False(filter.SortOptions.IsAscending);
        Assert.Equal("Test", filter.Name);

        var list = filter.Statements.ToList();
        AssertStatementSame(list[2], SeriesFilterField.SeriesName, FilterComparison.Matches, "a");
        AssertStatementSame(list[1], SeriesFilterField.AgeRating, FilterComparison.Equal, (int) AgeRating.Unknown + string.Empty);
        AssertStatementSame(list[0], SeriesFilterField.Genres, FilterComparison.Equal, "95");
    }

    [Fact]
    public void Test_Decode2()
    {
        const string sep = SmartFilterHelper.StatementSeparator;
        const string encoded = "name=Test%202&stmts=" +
                               "comparison%253D10%25C2%25A6field%253D1%25C2%25A6value%253DA" + sep +
                               "comparison%253D0%25C2%25A6field%253D19%25C2%25A6value%253D11" +
                               "&sortOptions=sortField%3D1%C2%A6isAscending%3DTrue&limitTo=0&combination=1";

        var filter = (SeriesFilterV2Dto) SmartFilterHelper.Decode(encoded);
        Assert.NotNull(filter.SortOptions);
        Assert.True(filter.SortOptions.IsAscending);
    }

    [Fact]
    public void Test_Decode_ReadingList()
    {
        const string encoded =
            "entityType=ReadingList" +
            "&stmts=comparison%253D5%25C2%25A6field%253D4%25C2%25A6value%253D7" +
            "&sortOptions=sortField%3D1%C2%A6isAscending%3DTrue" +
            "&limitTo=0&combination=1";

        var filter = (ReadingListFilterDto) SmartFilterHelper.Decode(encoded);

        Assert.Equal(FilterEntityType.ReadingList, filter.EntityType);
        Assert.Equal(0, filter.LimitTo);
        Assert.Equal(FilterCombination.And, filter.Combination);
        Assert.NotNull(filter.SortOptions);
        Assert.Equal(ReadingListSortField.Title, filter.SortOptions.SortField);
        Assert.True(filter.SortOptions.IsAscending);

        var list = filter.Statements.ToList();
        Assert.Single(list);
        AssertStatementSame(list[0], ReadingListFilterField.Tags, FilterComparison.Contains, "7");
    }

    [Fact]
    public void Test_Decode_MissingEntityType_DefaultsToSeries()
    {
        var filter = new SeriesFilterV2Dto()
        {
            Name = "NoEntityType",
            SortOptions = new SeriesSortOptionDto() { IsAscending = true, SortField = SeriesSortField.SortName },
            LimitTo = 3,
            Combination = FilterCombination.And,
            Statements = new List<SeriesFilterStatementDto>()
            {
                new() { Comparison = FilterComparison.Matches, Field = SeriesFilterField.SeriesName, Value = "test" }
            }
        };

        var encoded = SmartFilterHelper.Encode(filter);
        var strippedEncoded = encoded.Replace("entityType=Series&", string.Empty);

        var decoded = SmartFilterHelper.Decode(strippedEncoded);

        Assert.IsType<SeriesFilterV2Dto>(decoded);
        var typedDecoded = (SeriesFilterV2Dto) decoded;
        Assert.Equal("NoEntityType", typedDecoded.Name);
        Assert.Single(typedDecoded.Statements);
        AssertStatementSame(typedDecoded.Statements.First(), filter.Statements.First());
    }

    [Fact]
    public void Test_EncodeDecode()
    {
        var filter = new SeriesFilterV2Dto()
        {
            Name = "Test",
            SortOptions = new SeriesSortOptionDto() {
                IsAscending = false,
                SortField = SeriesSortField.CreatedDate
                },
            LimitTo = 10,
            Combination = FilterCombination.And,
            Statements = new List<SeriesFilterStatementDto>()
            {
                new SeriesFilterStatementDto()
                {
                    Comparison = FilterComparison.Equal,
                    Field = SeriesFilterField.AgeRating,
                    Value = (int) AgeRating.Unknown + string.Empty
                }
            }
        };

        var encodedFilter = SmartFilterHelper.Encode(filter);

        var decoded = (SeriesFilterV2Dto) SmartFilterHelper.Decode(encodedFilter);
        Assert.Single(decoded.Statements);
        AssertStatementSame(decoded.Statements.First(), filter.Statements.First());

        Assert.Equal("Test", decoded.Name);
        Assert.Equal(10, decoded.LimitTo);
        Assert.NotNull(decoded.SortOptions);
        Assert.Equal(SeriesSortField.CreatedDate, decoded.SortOptions.SortField);
        Assert.False(decoded.SortOptions.IsAscending);
    }

    [Fact]
    public void Test_EncodeDecode_MultipleValues_Contains()
    {
        var filter = new SeriesFilterV2Dto()
        {
            Name = "Test",
            SortOptions = new SeriesSortOptionDto() {
                IsAscending = false,
                SortField = SeriesSortField.CreatedDate
            },
            LimitTo = 10,
            Combination = FilterCombination.And,
            Statements = new List<SeriesFilterStatementDto>()
            {
                new SeriesFilterStatementDto()
                {
                    Comparison = FilterComparison.Equal,
                    Field = SeriesFilterField.AgeRating,
                    Value = $"{(int) AgeRating.Unknown + string.Empty},{(int) AgeRating.G + string.Empty}"
                }
            }
        };

        var encodedFilter = SmartFilterHelper.Encode(filter);
        var decoded = (SeriesFilterV2Dto) SmartFilterHelper.Decode(encodedFilter);

        Assert.Single(decoded.Statements);
        AssertStatementSame(decoded.Statements.First(), filter.Statements.First());

        Assert.Equal(2, decoded.Statements.First().Value.Split(",").Length);

        Assert.Equal("Test", decoded.Name);
        Assert.Equal(10, decoded.LimitTo);
        Assert.NotNull(decoded.SortOptions);
        Assert.Equal(SeriesSortField.CreatedDate, decoded.SortOptions.SortField);
        Assert.False(decoded.SortOptions.IsAscending);
    }

    [Fact]
    public void Test_EncodeDecode_ReadingList()
    {
        var filter = new ReadingListFilterDto()
        {
            SortOptions = new ReadingListSortOptionDto() { IsAscending = true, SortField = ReadingListSortField.Title },
            LimitTo = 5,
            Combination = FilterCombination.Or,
            Statements = new List<ReadingListFilterStatementDto>()
            {
                new() { Comparison = FilterComparison.Matches, Field = ReadingListFilterField.Title, Value = "Manga" }
            }
        };

        var encoded = SmartFilterHelper.Encode(filter);
        var decoded = (ReadingListFilterDto) SmartFilterHelper.Decode(encoded);

        Assert.Equal(FilterEntityType.ReadingList, decoded.EntityType);
        Assert.Equal(5, decoded.LimitTo);
        Assert.Equal(FilterCombination.Or, decoded.Combination);
        Assert.Single(decoded.Statements);
        AssertStatementSame(decoded.Statements.First(), filter.Statements.First());
        Assert.NotNull(decoded.SortOptions);
        Assert.Equal(ReadingListSortField.Title, decoded.SortOptions.SortField);
        Assert.True(decoded.SortOptions.IsAscending);
    }

    [Fact]
    public void Test_EncodeDecode_Person()
    {
        var filter = new PersonFilterDto()
        {
            SortOptions = new PersonSortOptionDto() { IsAscending = false, SortField = PersonSortField.SeriesCount },
            LimitTo = 0,
            Combination = FilterCombination.And,
            Statements = new List<PersonFilterStatementDto>()
            {
                new() { Comparison = FilterComparison.Matches, Field = PersonFilterField.Name, Value = "CLAMP" }
            }
        };

        var encoded = SmartFilterHelper.Encode(filter);
        var decoded = (PersonFilterDto) SmartFilterHelper.Decode(encoded);

        Assert.Equal(FilterEntityType.Person, decoded.EntityType);
        Assert.Single(decoded.Statements);
        AssertStatementSame(decoded.Statements.First(), filter.Statements.First());
        Assert.NotNull(decoded.SortOptions);
        Assert.Equal(PersonSortField.SeriesCount, decoded.SortOptions.SortField);
        Assert.False(decoded.SortOptions.IsAscending);
    }

    [Fact]
    public void Test_EncodeDecode_Annotation()
    {
        var filter = new AnnotationFilterDto()
        {
            SortOptions = new AnnotationSortOptionDto() { IsAscending = true, SortField = AnnotationSortField.Created },
            LimitTo = 0,
            Combination = FilterCombination.And,
            Statements = new List<AnnotationFilterStatementDto>()
            {
                new() { Comparison = FilterComparison.Contains, Field = AnnotationFilterField.Comment, Value = "important" }
            }
        };

        var encoded = SmartFilterHelper.Encode(filter);
        var decoded = (AnnotationFilterDto) SmartFilterHelper.Decode(encoded);

        Assert.Equal(FilterEntityType.Annotation, decoded.EntityType);
        Assert.Single(decoded.Statements);
        AssertStatementSame(decoded.Statements.First(), filter.Statements.First());
        Assert.NotNull(decoded.SortOptions);
        Assert.Equal(AnnotationSortField.Created, decoded.SortOptions.SortField);
        Assert.True(decoded.SortOptions.IsAscending);
    }

    private static void AssertStatementSame(SeriesFilterStatementDto statement, SeriesFilterStatementDto statement2)
    {
        Assert.Equal(statement.Field, statement2.Field);
        Assert.Equal(statement.Comparison, statement2.Comparison);
        Assert.Equal(statement.Value, statement2.Value);
    }

    private static void AssertStatementSame(SeriesFilterStatementDto statement, SeriesFilterField field, FilterComparison combination, string value)
    {
        Assert.Equal(statement.Field, field);
        Assert.Equal(statement.Comparison, combination);
        Assert.Equal(statement.Value, value);
    }

    private static void AssertStatementSame(ReadingListFilterStatementDto statement, ReadingListFilterField field, FilterComparison comparison, string value)
    {
        Assert.Equal(statement.Field, field);
        Assert.Equal(statement.Comparison, comparison);
        Assert.Equal(statement.Value, value);
    }

    private static void AssertStatementSame(ReadingListFilterStatementDto a, ReadingListFilterStatementDto b)
    {
        Assert.Equal(a.Field, b.Field);
        Assert.Equal(a.Comparison, b.Comparison);
        Assert.Equal(a.Value, b.Value);
    }

    private static void AssertStatementSame(PersonFilterStatementDto a, PersonFilterStatementDto b)
    {
        Assert.Equal(a.Field, b.Field);
        Assert.Equal(a.Comparison, b.Comparison);
        Assert.Equal(a.Value, b.Value);
    }

    private static void AssertStatementSame(AnnotationFilterStatementDto a, AnnotationFilterStatementDto b)
    {
        Assert.Equal(a.Field, b.Field);
        Assert.Equal(a.Comparison, b.Comparison);
        Assert.Equal(a.Value, b.Value);
    }
}
