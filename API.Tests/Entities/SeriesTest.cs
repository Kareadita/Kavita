using System;
using API.Data;
using API.Tests.Helpers;
using Xunit;

namespace API.Tests.Entities
{
    /// <summary>
    /// Tests for <see cref="API.Entities.Series"/>
    /// </summary>
    public class SeriesTest
    {
        [Theory]
        [InlineData("")]
        public void CreateSeries(string name)
        {
            var key = API.Parser.Parser.Normalize(name);
            var series = DbFactory.Series(name);
            Assert.Equal(0, series.Id);
            Assert.Equal(0, series.Pages);
            Assert.Equal(name, series.Name);
            Assert.Equal(Array.Empty<byte>(), series.CoverImage);
            Assert.Equal(name, series.LocalizedName);
            Assert.Equal(name, series.SortName);
            Assert.Equal(name, series.OriginalName);
            Assert.Equal(key, series.NormalizedName);
        }
        
        [Fact]
        public void MergeTest_Should_NotTakeProperties()
        {
            var name = "Darker than Black";
            var series = DbFactory.Series(name);
            var series2 = DbFactory.Series("darker than black");
            series2.Pages = 100;
            
            series.Merge(series2);
            Assert.Equal(name, series.OriginalName); 
            Assert.Equal(name, series.Name); 
            Assert.Equal(series2.Pages, series.Pages); 
            Assert.NotEqual(series2.NormalizedName, series.NormalizedName); 
        }

        [Fact]
        public void MergeTest_Should_TakeFromSecond()
        {
            var name = "Darker than Black";
            var series = DbFactory.Series(name);
            var series2 = DbFactory.Series("darker than black");
            series2.Pages = 100;
            
            series.Merge(series2);
            Assert.Equal(name, series.OriginalName); 
            Assert.Equal(name, series.Name); 
            Assert.Equal(series2.Pages, series.Pages); 
            Assert.Equal(API.Parser.Parser.Normalize(name), series.NormalizedName); 
        }
    }
}