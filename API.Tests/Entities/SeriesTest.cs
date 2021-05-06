using API.Data;
using Xunit;

namespace API.Tests.Entities
{
    /// <summary>
    /// Tests for <see cref="API.Entities.Series"/>
    /// </summary>
    public class SeriesTest
    {
        [Theory]
        [InlineData("Darker than Black")]
        public void CreateSeries(string name)
        {
            var key = API.Parser.Parser.Normalize(name);
            var series = DbFactory.Series(name);
            Assert.Equal(0, series.Id);
            Assert.Equal(0, series.Pages);
            Assert.Equal(name, series.Name);
            Assert.Null(series.CoverImage);
            Assert.Equal(name, series.LocalizedName);
            Assert.Equal(name, series.SortName);
            Assert.Equal(name, series.OriginalName);
            Assert.Equal(key, series.NormalizedName);
        }
    }
}