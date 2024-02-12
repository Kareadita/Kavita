using System;
using System.Collections.Generic;
using System.Linq;
using API.DTOs;
using API.Extensions;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Order;

namespace API.Benchmark;

/// <summary>
/// This is used as a scratchpad for testing
/// </summary>
[MemoryDiagnoser]
[Orderer(SummaryOrderPolicy.FastestToSlowest)]
[RankColumn]
public class TestBenchmark
{
    private static IEnumerable<VolumeDto> GenerateVolumes(int max)
    {
        var random = new Random();
        var maxIterations = random.Next(max) + 1;
        var list = new List<VolumeDto>();
        for (var i = 0; i < maxIterations; i++)
        {
            list.Add(new VolumeDto()
            {
                MinNumber = random.Next(10) > 5 ? 1 : 0,
                Chapters = GenerateChapters()
            });
        }

        return list;
    }

    private static List<ChapterDto> GenerateChapters()
    {
        var list =  new List<ChapterDto>();
        for (var i = 1; i < 40; i++)
        {
            list.Add(new ChapterDto()
            {
                Range = i + string.Empty
            });
        }

        return list;
    }

    private static void SortSpecialChapters(IEnumerable<VolumeDto> volumes)
    {
        foreach (var v in volumes.WhereNotLooseLeaf())
        {
            v.Chapters = v.Chapters.OrderByNatural(x => x.Range).ToList();
        }
    }

    [Benchmark]
    public void TestSortSpecialChapters()
    {
        var volumes = GenerateVolumes(10);
        SortSpecialChapters(volumes);
    }

}
