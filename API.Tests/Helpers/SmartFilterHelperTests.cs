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
        var encoded = """
                      stmts=comparison%3D5%26field%3D18%26value%3D6%2Ccomparison%3D0%26field%3D4%26value%3D0%2Ccomparison%3D7%26field%3D1%26value%3Da&sortOptions=sortField=1&isAscending=true&limitTo=0&combination=1
                      """;

        var filter = SmartFilterHelper.Decode(encoded);

        Assert.Equal(0, filter.LimitTo);
        Assert.Equal(SortField.SortName, filter.SortOptions.SortField);
        Assert.True(filter.SortOptions.IsAscending);
        Assert.Null(filter.Name);

        var list = filter.Statements.ToList();
        AssertStatementSame(list[2], FilterField.SeriesName, FilterComparison.Matches, "a");
        AssertStatementSame(list[1], FilterField.AgeRating, FilterComparison.Equal, (int) AgeRating.Unknown + "");
        AssertStatementSame(list[0], FilterField.Genres, FilterComparison.Contains, "6");

    }

    private void AssertStatementSame(FilterStatementDto statement, FilterField field, FilterComparison combination, string value)
    {
        Assert.Equal(statement.Field, field);
        Assert.Equal(statement.Comparison, combination);
        Assert.Equal(statement.Value, value);
    }

}
