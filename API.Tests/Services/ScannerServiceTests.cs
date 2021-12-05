using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data.Common;
using System.IO;
using System.IO.Abstractions.TestingHelpers;
using System.Linq;
using System.Threading.Tasks;
using API.Data;
using API.Entities;
using API.Entities.Enums;
using API.Entities.Metadata;
using API.Helpers;
using API.Parser;
using API.Services;
using API.Services.Tasks;
using API.Services.Tasks.Scanner;
using API.SignalR;
using AutoMapper;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace API.Tests.Services
{
    public class ScannerServiceTests
    {
        [Fact]
        public void AddOrUpdateFileForChapter()
        {
            // TODO: This can be tested, it has _filesystem mocked
        }

        [Fact]
        public void FindSeriesNotOnDisk_Should_RemoveNothing_Test()
        {
            var infos = new Dictionary<ParsedSeries, List<ParserInfo>>();

            AddToParsedInfo(infos, new ParserInfo() {Series = "Darker than Black", Format = MangaFormat.Archive});
            AddToParsedInfo(infos, new ParserInfo() {Series = "Cage of Eden", Volumes = "1", Format = MangaFormat.Archive});
            AddToParsedInfo(infos, new ParserInfo() {Series = "Cage of Eden", Volumes = "10", Format = MangaFormat.Archive});

            var existingSeries = new List<Series>
            {
                new Series()
                {
                    Name = "Cage of Eden",
                    LocalizedName = "Cage of Eden",
                    OriginalName = "Cage of Eden",
                    NormalizedName = API.Parser.Parser.Normalize("Cage of Eden"),
                    Metadata = new SeriesMetadata(),
                    Format = MangaFormat.Archive
                },
                new Series()
                {
                    Name = "Darker Than Black",
                    LocalizedName = "Darker Than Black",
                    OriginalName = "Darker Than Black",
                    NormalizedName = API.Parser.Parser.Normalize("Darker Than Black"),
                    Metadata = new SeriesMetadata(),
                    Format = MangaFormat.Archive
                }
            };



            Assert.Empty(ScannerService.FindSeriesNotOnDisk(existingSeries, infos));
        }


        // TODO: Figure out how to do this with ParseScannedFiles
        // [Theory]
        // [InlineData(new [] {"Darker than Black"}, "Darker than Black", "Darker than Black")]
        // [InlineData(new [] {"Darker than Black"}, "Darker Than Black", "Darker than Black")]
        // [InlineData(new [] {"Darker than Black"}, "Darker Than Black!", "Darker than Black")]
        // [InlineData(new [] {""}, "Runaway Jack", "Runaway Jack")]
        // public void MergeNameTest(string[] existingSeriesNames, string parsedInfoName, string expected)
        // {
        //     var collectedSeries = new ConcurrentDictionary<ParsedSeries, List<ParserInfo>>();
        //     foreach (var seriesName in existingSeriesNames)
        //     {
        //         AddToParsedInfo(collectedSeries, new ParserInfo() {Series = seriesName, Format = MangaFormat.Archive});
        //     }
        //
        //     var actualName = new ParseScannedFiles(_bookService, _logger).MergeName(collectedSeries, new ParserInfo()
        //     {
        //         Series = parsedInfoName,
        //         Format = MangaFormat.Archive
        //     });
        //
        //     Assert.Equal(expected, actualName);
        // }

        // [Fact]
        // public void RemoveMissingSeries_Should_RemoveSeries()
        // {
        //     var existingSeries = new List<Series>()
        //     {
        //         EntityFactory.CreateSeries("Darker than Black Vol 1"),
        //         EntityFactory.CreateSeries("Darker than Black"),
        //         EntityFactory.CreateSeries("Beastars"),
        //     };
        //     var missingSeries = new List<Series>()
        //     {
        //         EntityFactory.CreateSeries("Darker than Black Vol 1"),
        //     };
        //     existingSeries = ScannerService.RemoveMissingSeries(existingSeries, missingSeries, out var removeCount).ToList();
        //
        //     Assert.DoesNotContain(missingSeries[0].Name, existingSeries.Select(s => s.Name));
        //     Assert.Equal(missingSeries.Count, removeCount);
        // }

        private void AddToParsedInfo(IDictionary<ParsedSeries, List<ParserInfo>> collectedSeries, ParserInfo info)
        {
            var existingKey = collectedSeries.Keys.FirstOrDefault(ps =>
                ps.Format == info.Format && ps.NormalizedName == API.Parser.Parser.Normalize(info.Series));
            existingKey ??= new ParsedSeries()
            {
                Format = info.Format,
                Name = info.Series,
                NormalizedName = API.Parser.Parser.Normalize(info.Series)
            };
            if (collectedSeries.GetType() == typeof(ConcurrentDictionary<,>))
            {
                ((ConcurrentDictionary<ParsedSeries, List<ParserInfo>>) collectedSeries).AddOrUpdate(existingKey, new List<ParserInfo>() {info}, (_, oldValue) =>
                {
                    oldValue ??= new List<ParserInfo>();
                    if (!oldValue.Contains(info))
                    {
                        oldValue.Add(info);
                    }

                    return oldValue;
                });
            }
            else
            {
                if (!collectedSeries.ContainsKey(existingKey))
                {
                    collectedSeries.Add(existingKey, new List<ParserInfo>() {info});
                }
                else
                {
                    var list = collectedSeries[existingKey];
                    if (!list.Contains(info))
                    {
                        list.Add(info);
                    }

                    collectedSeries[existingKey] = list;
                }

            }

        }
    }
}
