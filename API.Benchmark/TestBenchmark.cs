using System;
using System.Collections.Generic;
using System.Linq;
using API.Comparators;
using API.DTOs;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Order;

namespace API.Benchmark
{
    /// <summary>
    /// This is used as a scratchpad for testing
    /// </summary>
    [MemoryDiagnoser]
    [Orderer(SummaryOrderPolicy.FastestToSlowest)]
    [RankColumn]
    public class TestBenchmark
    {
        private readonly NaturalSortComparer _naturalSortComparer = new ();


        private List<VolumeDto> GenerateVolumes(int max)
        {
            var random = new Random();
            var maxIterations = random.Next(max) + 1;
            var list = new List<VolumeDto>();
            for (var i = 0; i < maxIterations; i++)
            {
                list.Add(new VolumeDto()
                {
                    Number = random.Next(10) > 5 ? 1 : 0,
                    Chapters = GenerateChapters()
                });
            }

            return list;
        }

        private List<ChapterDto> GenerateChapters()
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

        private void SortSpecialChapters(IEnumerable<VolumeDto> volumes)
        {
            foreach (var v in volumes.Where(vDto => vDto.Number == 0))
            {
                v.Chapters = v.Chapters.OrderBy(x => x.Range, _naturalSortComparer).ToList();
            }
        }

        [Benchmark]
        public void TestSortSpecialChapters()
        {
            var volumes = GenerateVolumes(10);
            SortSpecialChapters(volumes);
        }

    }
}
