using API.Entities;
using API.Extensions;
using Xunit;

namespace API.Tests.Extensions
{
    public class SeriesExtensionsTests
    {
        [Theory]
        [InlineData(new [] {"Darker than Black", "Darker Than Black", "Darker than Black"}, new [] {"Darker than Black"}, true)]
        [InlineData(new [] {"Darker than Black", "Darker Than Black", "Darker than Black"}, new [] {"Darker_than_Black"}, true)]
        [InlineData(new [] {"Darker than Black", "Darker Than Black", "Darker than Black"}, new [] {"Darker then Black!"}, false)]
        public void NameInListTest(string[] seriesInput, string[] list, bool expected)
        {
            var series = new Series()
            {
                Name = seriesInput[0],
                LocalizedName = seriesInput[1],
                OriginalName = seriesInput[2],
                NormalizedName = Parser.Parser.Normalize(seriesInput[0])
            };
            
            Assert.Equal(expected, series.NameInList(list));
        }
    }
}