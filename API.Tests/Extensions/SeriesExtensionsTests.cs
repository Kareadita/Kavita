﻿using API.Entities;
using API.Extensions;
using API.Parser;
using Xunit;

namespace API.Tests.Extensions
{
    public class SeriesExtensionsTests
    {
        [Theory]
        [InlineData(new [] {"Darker than Black", "Darker Than Black", "Darker than Black"}, new [] {"Darker than Black"}, true)]
        [InlineData(new [] {"Darker than Black", "Darker Than Black", "Darker than Black"}, new [] {"Darker_than_Black"}, true)]
        [InlineData(new [] {"Darker than Black", "Darker Than Black", "Darker than Black"}, new [] {"Darker then Black!"}, false)]
        [InlineData(new [] {"Salem's Lot", "Salem's Lot", "Salem's Lot"}, new [] {"Salem's Lot"}, true)]
        [InlineData(new [] {"Salem's Lot", "Salem's Lot", "Salem's Lot"}, new [] {"salems lot"}, true)]
        [InlineData(new [] {"Salem's Lot", "Salem's Lot", "Salem's Lot"}, new [] {"salem's lot"}, true)]
        // Different normalizations pass as we check normalization against an on-the-fly calculation so we don't delete series just because we change how normalization works
        [InlineData(new [] {"Salem's Lot", "Salem's Lot", "Salem's Lot", "salems lot"}, new [] {"salem's lot"}, true)]
        [InlineData(new [] {"Rent-a-Girlfriend", "Rent-a-Girlfriend", "Kanojo, Okarishimasu", "rentagirlfriend"}, new [] {"Kanojo, Okarishimasu"}, true)]
        public void NameInListTest(string[] seriesInput, string[] list, bool expected)
        {
            var series = new Series()
            {
                Name = seriesInput[0],
                LocalizedName = seriesInput[1],
                OriginalName = seriesInput[2],
                NormalizedName = seriesInput.Length == 4 ? seriesInput[3] : API.Parser.Parser.Normalize(seriesInput[0]),
                Metadata = new SeriesMetadata()
            };

            Assert.Equal(expected, series.NameInList(list));
        }

        [Theory]
        [InlineData(new [] {"Darker than Black", "Darker Than Black", "Darker than Black"}, "Darker than Black", true)]
        [InlineData(new [] {"Rent-a-Girlfriend", "Rent-a-Girlfriend", "Kanojo, Okarishimasu", "rentagirlfriend"}, "Kanojo, Okarishimasu", true)]
        [InlineData(new [] {"Rent-a-Girlfriend", "Rent-a-Girlfriend", "Kanojo, Okarishimasu", "rentagirlfriend"}, "Rent", false)]
        public void NameInParserInfoTest(string[] seriesInput, string parserSeries, bool expected)
        {
            var series = new Series()
            {
                Name = seriesInput[0],
                LocalizedName = seriesInput[1],
                OriginalName = seriesInput[2],
                NormalizedName = seriesInput.Length == 4 ? seriesInput[3] : API.Parser.Parser.Normalize(seriesInput[0]),
                Metadata = new SeriesMetadata()
            };
            var info = new ParserInfo();
            info.Series = parserSeries;

            Assert.Equal(expected, series.NameInParserInfo(info));
        }


    }
}
