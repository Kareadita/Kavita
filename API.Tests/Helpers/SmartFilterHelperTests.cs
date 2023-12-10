using System;
using System.Collections.Generic;
using System.Linq;
using API.Data.ManualMigrations;
using API.DTOs.Filtering;
using API.DTOs.Filtering.v2;
using API.Entities.Enums;
using API.Helpers;
using Xunit;

namespace API.Tests.Helpers;

public class SmartFilterHelperTests
{

    [Theory]
    [InlineData("", false)]
    [InlineData("name=DC%20-%20On%20Deck&stmts=comparison%3D1%26field%3D20%26value%3D0,comparison%3D9%26field%3D20%26value%3D100,comparison%3D0%26field%3D19%26value%3D274&sortOptions=sortField%3D1&isAscending=True&limitTo=0&combination=1", true)]
    [InlineData("name=English%20In%20Progress&stmts=comparison%253D8%252Cfield%253D7%252Cvalue%253D4%25252C3,comparison%253D3%252Cfield%253D20%252Cvalue%253D100,comparison%253D8%252Cfield%253D3%252Cvalue%253Dja,comparison%253D1%252Cfield%253D20%252Cvalue%253D0&sortOptions=sortField%3D7,isAscending%3DFalse&limitTo=0&combination=1", true)]
    [InlineData("name=Unread%20Isekai%20Light%20Novels&stmts=comparison%253D0%25C2%25A6field%253D20%25C2%25A6value%253D0%EF%BF%BDcomparison%253D5%25C2%25A6field%253D6%25C2%25A6value%253D230%EF%BF%BDcomparison%253D8%25C2%25A6field%253D7%25C2%25A6value%253D4%EF%BF%BDcomparison%253D0%25C2%25A6field%253D19%25C2%25A6value%253D14&sortOptions=sortField%3D5%C2%A6isAscending%3DFalse&limitTo=0&combination=1", false)]
    [InlineData("name=Zero&stmts=comparison%3d7%26field%3d1%26value%3d0&sortOptions=sortField=2&isAscending=False&limitTo=0&combination=1", true)]
    public void Test_ShouldMigrateFilter(string filter, bool expected)
    {
        Assert.Equal(expected, MigrateSmartFilterEncoding.ShouldMigrateFilter(filter));
    }

    [Fact]
    public void Test_Decode()
    {
        const string encoded = """
                               name=Test&stmts=comparison%253D0%25C2%25A6field%253D18%25C2%25A6value%253D95�comparison%253D0%25C2%25A6field%253D4%25C2%25A6value%253D0�comparison%253D7%25C2%25A6field%253D1%25C2%25A6value%253Da&sortOptions=sortField%3D2¦isAscending%3DFalse&limitTo=10&combination=1
                               """;

        var filter = SmartFilterHelper.Decode(encoded);

        Assert.Equal(10, filter.LimitTo);
        Assert.Equal(SortField.CreatedDate, filter.SortOptions.SortField);
        Assert.False(filter.SortOptions.IsAscending);
        Assert.Equal("Test" , filter.Name);

        var list = filter.Statements.ToList();
        AssertStatementSame(list[2], FilterField.SeriesName, FilterComparison.Matches, "a");
        AssertStatementSame(list[1], FilterField.AgeRating, FilterComparison.Equal, (int) AgeRating.Unknown + string.Empty);
        AssertStatementSame(list[0], FilterField.Genres, FilterComparison.Equal, "95");
    }

    [Fact]
    public void Test_Decode2()
    {
        const string encoded = """
                               name=Test%202&stmts=comparison%253D10%25C2%25A6field%253D1%25C2%25A6value%253DA%EF%BF%BDcomparison%253D0%25C2%25A6field%253D19%25C2%25A6value%253D11&sortOptions=sortField%3D1%C2%A6isAscending%3DTrue&limitTo=0&combination=1
                               """;

        var filter = SmartFilterHelper.Decode(encoded);
        Assert.True(filter.SortOptions.IsAscending);
    }

    [Fact]
    public void Test_EncodeDecode()
    {
        var filter = new FilterV2Dto()
        {
            Name = "Test",
            SortOptions = new SortOptions() {
                IsAscending = false,
                SortField = SortField.CreatedDate
                },
            LimitTo = 10,
            Combination = FilterCombination.And,
            Statements = new List<FilterStatementDto>()
            {
                new FilterStatementDto()
                {
                    Comparison = FilterComparison.Equal,
                    Field = FilterField.AgeRating,
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
        Assert.Equal(SortField.CreatedDate, decoded.SortOptions.SortField);
        Assert.False(decoded.SortOptions.IsAscending);
    }

    [Fact]
    public void Test_EncodeDecode_MultipleValues_Contains()
    {
        var filter = new FilterV2Dto()
        {
            Name = "Test",
            SortOptions = new SortOptions() {
                IsAscending = false,
                SortField = SortField.CreatedDate
            },
            LimitTo = 10,
            Combination = FilterCombination.And,
            Statements = new List<FilterStatementDto>()
            {
                new FilterStatementDto()
                {
                    Comparison = FilterComparison.Equal,
                    Field = FilterField.AgeRating,
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
        Assert.Equal(SortField.CreatedDate, decoded.SortOptions.SortField);
        Assert.False(decoded.SortOptions.IsAscending);
    }

    [Theory]
    [InlineData("name=DC%20-%20On%20Deck&stmts=comparison%3D1%26field%3D20%26value%3D0,comparison%3D9%26field%3D20%26value%3D100,comparison%3D0%26field%3D19%26value%3D274&sortOptions=sortField%3D1&isAscending=True&limitTo=0&combination=1")]
    [InlineData("name=Manga%20-%20On%20Deck&stmts=comparison%253D1%252Cfield%253D20%252Cvalue%253D0,comparison%253D3%252Cfield%253D20%252Cvalue%253D100,comparison%253D0%252Cfield%253D19%252Cvalue%253D2&sortOptions=sortField%3D1,isAscending%3DTrue&limitTo=0&combination=1")]
    [InlineData("name=English%20In%20Progress&stmts=comparison%253D8%252Cfield%253D7%252Cvalue%253D4%25252C3,comparison%253D3%252Cfield%253D20%252Cvalue%253D100,comparison%253D8%252Cfield%253D3%252Cvalue%253Dja,comparison%253D1%252Cfield%253D20%252Cvalue%253D0&sortOptions=sortField%3D7,isAscending%3DFalse&limitTo=0&combination=1")]
    public void MigrationWorks(string filter)
    {
        try
        {
            var updatedFilter = MigrateSmartFilterEncoding.EncodeFix(filter);
            Assert.NotNull(updatedFilter);
        }
        catch (Exception ex)
        {
            Assert.Fail("Exception thrown: " + ex.Message);
        }

    }

    private static void AssertStatementSame(FilterStatementDto statement, FilterStatementDto statement2)
    {
        Assert.Equal(statement.Field, statement2.Field);
        Assert.Equal(statement.Comparison, statement2.Comparison);
        Assert.Equal(statement.Value, statement2.Value);
    }

    private static void AssertStatementSame(FilterStatementDto statement, FilterField field, FilterComparison combination, string value)
    {
        Assert.Equal(statement.Field, field);
        Assert.Equal(statement.Comparison, combination);
        Assert.Equal(statement.Value, value);
    }

}
