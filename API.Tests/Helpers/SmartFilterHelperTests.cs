using System;
using System.Collections.Generic;
using System.Linq;
using API.DTOs.Filtering;
using API.DTOs.Filtering.v2;
using API.Entities.Enums;
using API.Helpers;
using Xunit;

namespace API.Tests.Helpers;

public class SmartFilterHelperTests
{
    [Fact]
    public void Test_Decode()
    {
        const string encoded = """
                               stmts=comparison%3D5¦field%3D18¦value%3D95�comparison%3D0¦field%3D4¦value%3D0�comparison%3D7¦field%3D1¦value%3Da&sortOptions=sortField=2,isAscending=false&limitTo=0&combination=1
                               """;

        var filter = SmartFilterHelper.Decode(encoded);

        Assert.Equal(0, filter.LimitTo);
        Assert.Equal(SortField.CreatedDate, filter.SortOptions.SortField);
        Assert.False(filter.SortOptions.IsAscending);
        Assert.Null(filter.Name);

        var list = filter.Statements.ToList();
        AssertStatementSame(list[2], FilterField.SeriesName, FilterComparison.Matches, "a");
        AssertStatementSame(list[1], FilterField.AgeRating, FilterComparison.Equal, (int) AgeRating.Unknown + string.Empty);
        AssertStatementSame(list[0], FilterField.Genres, FilterComparison.Contains, "95");
    }

    [Fact]
    public void Test_Encode()
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
