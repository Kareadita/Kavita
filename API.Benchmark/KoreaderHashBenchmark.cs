using API.Helpers.Builders;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Order;
using System;
using API.Entities.Enums;

namespace API.Benchmark
{
    [StopOnFirstError]
    [MemoryDiagnoser]
    [RankColumn]
    [Orderer(SummaryOrderPolicy.FastestToSlowest)]
    [SimpleJob(launchCount: 1, warmupCount: 5, invocationCount: 20)]
    public class KoreaderHashBenchmark
    {
        private const string sourceEpub = "./Data/AesopsFables.epub";

        [Benchmark(Baseline = true)]
        public void TestBuildManga_baseline()
        {
            var file = new MangaFileBuilder(sourceEpub, MangaFormat.Epub)
                .Build();
            if (file == null)
            {
                throw new Exception("Failed to build manga file");
            }
        }

        [Benchmark]
        public void TestBuildManga_withHash()
        {
            var file = new MangaFileBuilder(sourceEpub, MangaFormat.Epub)
                .WithHash()
                .Build();
            if (file == null)
            {
                throw new Exception("Failed to build manga file");
            }
        }
    }
}
