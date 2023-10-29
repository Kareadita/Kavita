using System;
using System.Collections.Generic;
using System.Linq;
using API.DTOs.Filtering;
using API.DTOs.Filtering.v2;
using API.Entities.Enums;
using API.Helpers;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Utilities;
using Xunit;

namespace API.Tests.Helpers;

public class SmartFilterHelperTests
{
    [Fact]
    public void Test_Decode()
    {
        var encoded = """
                      stmts=comparison%3D5%26field%3D18%26value%3D95%2Ccomparison%3D0%26field%3D4%26value%3D0%2Ccomparison%3D7%26field%3D1%26value%3Da&sortOptions=sortField=2&isAscending=false&limitTo=10&combination=1
                      """;

        var filter = SmartFilterHelper.Decode(encoded);

        Assert.Equal(10, filter.LimitTo);
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
        Assert.Equal("name=Test&stmts=comparison%253D0%252Cfield%253D4%252Cvalue%253D0&sortOptions=sortField%3D2%2CisAscending%3DFalse&limitTo=10&combination=1", encodedFilter);
    }

    private void AssertStatementSame(FilterStatementDto statement, FilterField field, FilterComparison combination, string value)
    {
        Assert.Equal(statement.Field, field);
        Assert.Equal(statement.Comparison, combination);
        Assert.Equal(statement.Value, value);
    }

}
