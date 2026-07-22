using System.Linq;
using Kavita.Models.DTOs.Filtering.v2;
using Kavita.Models.DTOs.Filtering.v2.Requests;
using Kavita.Server.ManualMigrations.v0._9._1;
using Kavita.Services.Helpers.SmartFilter;

namespace Kavita.Server.Tests.ManualMigrations;

public class ManualMigrateReadLastSmartFilterComparisonTests
{
    private static string Encode(params SeriesFilterStatementDto[] statements)
    {
        var dto = new SeriesFilterV2Dto();
        foreach (var statement in statements)
        {
            dto.Statements.Add(statement);
        }

        return SmartFilterHelper.Encode(dto);
    }

    private static SeriesFilterStatementDto Statement(SeriesFilterField field, FilterComparison comparison, string value = "30")
    {
        return new SeriesFilterStatementDto
        {
            Field = field,
            Comparison = comparison,
            Value = value
        };
    }

    private static SeriesFilterV2Dto Decode(string encoded)
    {
        return (SeriesFilterV2Dto) SmartFilterHelper.Decode(encoded);
    }

    [Theory]
    [InlineData(FilterComparison.GreaterThan, FilterComparison.LessThan)]
    [InlineData(FilterComparison.LessThan, FilterComparison.GreaterThan)]
    [InlineData(FilterComparison.GreaterThanEqual, FilterComparison.LessThanEqual)]
    [InlineData(FilterComparison.LessThanEqual, FilterComparison.GreaterThanEqual)]
    public void FlipsNumericReadLastComparison(FilterComparison stored, FilterComparison expected)
    {
        var encoded = Encode(Statement(SeriesFilterField.ReadLast, stored));

        var migrated = ManualMigrateReadLastSmartFilterComparison.FlipReadLastComparisons(encoded);
        var dto = Decode(migrated);

        var statement = Assert.Single(dto.Statements);
        Assert.Equal(SeriesFilterField.ReadLast, statement.Field);
        Assert.Equal(expected, statement.Comparison);
        // Value must be preserved exactly.
        Assert.Equal("30", statement.Value);
    }

    [Theory]
    [InlineData(FilterComparison.Equal)]
    [InlineData(FilterComparison.NotEqual)]
    [InlineData(FilterComparison.IsAfter)]
    [InlineData(FilterComparison.IsBefore)]
    public void LeavesNonNumericReadLastComparisonUntouched(FilterComparison comparison)
    {
        var encoded = Encode(Statement(SeriesFilterField.ReadLast, comparison));

        var migrated = ManualMigrateReadLastSmartFilterComparison.FlipReadLastComparisons(encoded);

        // No numeric operator to flip -> the encoded string is returned unchanged.
        Assert.Equal(encoded, migrated);
        Assert.Equal(comparison, Assert.Single(Decode(migrated).Statements).Comparison);
    }

    [Fact]
    public void LeavesNonReadLastStatementsUntouched()
    {
        // A filter without any Read Last statement must round-trip unchanged.
        var encoded = Encode(
            Statement(SeriesFilterField.AgeRating, FilterComparison.GreaterThan, "10"),
            Statement(SeriesFilterField.ReadProgress, FilterComparison.LessThanEqual, "50"));

        var migrated = ManualMigrateReadLastSmartFilterComparison.FlipReadLastComparisons(encoded);

        Assert.Equal(encoded, migrated);
    }

    [Fact]
    public void OnlyFlipsReadLastStatementInAMixedFilter()
    {
        var encoded = Encode(
            Statement(SeriesFilterField.AgeRating, FilterComparison.GreaterThan, "10"),
            Statement(SeriesFilterField.ReadLast, FilterComparison.GreaterThan, "30"),
            Statement(SeriesFilterField.ReadProgress, FilterComparison.LessThan, "50"));

        var migrated = ManualMigrateReadLastSmartFilterComparison.FlipReadLastComparisons(encoded);
        var dto = Decode(migrated);

        var ageRating = dto.Statements.Single(s => s.Field == SeriesFilterField.AgeRating);
        var readLast = dto.Statements.Single(s => s.Field == SeriesFilterField.ReadLast);
        var readProgress = dto.Statements.Single(s => s.Field == SeriesFilterField.ReadProgress);

        // Only the Read Last statement flips; the others keep both their operator and value.
        Assert.Equal(FilterComparison.GreaterThan, ageRating.Comparison);
        Assert.Equal("10", ageRating.Value);
        Assert.Equal(FilterComparison.LessThan, readLast.Comparison);
        Assert.Equal("30", readLast.Value);
        Assert.Equal(FilterComparison.LessThan, readProgress.Comparison);
        Assert.Equal("50", readProgress.Value);
    }
}
