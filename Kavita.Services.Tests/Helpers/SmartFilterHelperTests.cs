using Kavita.Models.DTOs.Filtering;
using Kavita.Models.DTOs.Filtering.v2;
using Kavita.Models.DTOs.Filtering.v2.Requests;
using Kavita.Models.DTOs.Filtering.v2.SortFields;
using Kavita.Models.DTOs.Filtering.v2.SortOptions;
using Kavita.Models.Entities.Enums;
using Kavita.Services.Helpers;
using Kavita.Services.Helpers.SmartFilter;

namespace Kavita.Services.Tests.Helpers;

public class SmartFilterHelperTests
{

    [Fact]
    public void Test_Decode()
    {
        const string encoded = """
                               name=Test&stmts=comparison%253D0%25C2%25A6field%253D18%25C2%25A6value%253D95�comparison%253D0%25C2%25A6field%253D4%25C2%25A6value%253D0�comparison%253D7%25C2%25A6field%253D1%25C2%25A6value%253Da&sortOptions=sortField%3D2¦isAscending%3DFalse&limitTo=10&combination=1
                               """;

        var filter = SmartFilterHelper.Decode(encoded);

        Assert.Equal(10, filter.LimitTo);
        Assert.NotNull(filter.SortOptions);
        Assert.Equal(SeriesSortField.CreatedDate, filter.SortOptions.SortField);
        Assert.False(filter.SortOptions.IsAscending);
        Assert.Equal("Test" , filter.Name);

        var list = filter.Statements.ToList();
        AssertStatementSame(list[2], SeriesFilterField.SeriesName, FilterComparison.Matches, "a");
        AssertStatementSame(list[1], SeriesFilterField.AgeRating, FilterComparison.Equal, (int) AgeRating.Unknown + string.Empty);
        AssertStatementSame(list[0], SeriesFilterField.Genres, FilterComparison.Equal, "95");
    }

    [Fact]
    public void Test_Decode2()
    {
        const string encoded = """
                               name=Test%202&stmts=comparison%253D10%25C2%25A6field%253D1%25C2%25A6value%253DA%EF%BF%BDcomparison%253D0%25C2%25A6field%253D19%25C2%25A6value%253D11&sortOptions=sortField%3D1%C2%A6isAscending%3DTrue&limitTo=0&combination=1
                               """;

        var filter = SmartFilterHelper.Decode(encoded);
        Assert.NotNull(filter.SortOptions);
        Assert.True(filter.SortOptions.IsAscending);
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

        var decoded = SmartFilterHelper.Decode(encodedFilter);
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
        var decoded = SmartFilterHelper.Decode(encodedFilter);

        Assert.Single(decoded.Statements);
        AssertStatementSame(decoded.Statements.First(), filter.Statements.First());

        Assert.Equal(2, decoded.Statements.First().Value.Split(",").Length);

        Assert.Equal("Test", decoded.Name);
        Assert.Equal(10, decoded.LimitTo);
        Assert.NotNull(decoded.SortOptions);
        Assert.Equal(SeriesSortField.CreatedDate, decoded.SortOptions.SortField);
        Assert.False(decoded.SortOptions.IsAscending);
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

}
