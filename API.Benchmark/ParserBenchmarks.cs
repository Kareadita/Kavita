using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Order;

namespace API.Benchmark
{
    [MemoryDiagnoser]
    [Orderer(SummaryOrderPolicy.FastestToSlowest)]
    [RankColumn]
    public class ParserBenchmarks
    {
        private readonly IList<string> _names;

        private static readonly Regex NormalizeRegex = new Regex(@"[^a-zA-Z0-9]",
            RegexOptions.IgnoreCase | RegexOptions.Compiled,
            TimeSpan.FromMilliseconds(300));

        private static readonly Regex IsEpub = new Regex(@"\.epub",
            RegexOptions.IgnoreCase | RegexOptions.Compiled,
            TimeSpan.FromMilliseconds(300));

        public ParserBenchmarks()
        {
            // Read all series from SeriesNamesForNormalization.txt
            _names = File.ReadAllLines("Data/SeriesNamesForNormalization.txt");
            Console.WriteLine($"Performing benchmark on {_names.Count} series");
        }

        private static void NormalizeOriginal(string name)
        {
            Regex.Replace(name.ToLower(), "[^a-zA-Z0-9]", string.Empty);
        }

        private static void NormalizeNew(string name)
        {
            NormalizeRegex.Replace(name, string.Empty).ToLower();
        }


        [Benchmark]
        public void TestNormalizeName()
        {
            foreach (var name in _names)
            {
                NormalizeOriginal(name);
            }
        }


        [Benchmark]
        public void TestNormalizeName_New()
        {
            foreach (var name in _names)
            {
                NormalizeNew(name);
            }
        }

        [Benchmark]
        public void TestIsEpub()
        {
            foreach (var name in _names)
            {
                if ((name + ".epub").ToLower() == ".epub")
                {
                    /* No Operation */
                }
            }
        }

        [Benchmark]
        public void TestIsEpub_New()
        {
            foreach (var name in _names)
            {

                if (IsEpub.IsMatch((name + ".epub")))
                {
                    /* No Operation */
                }
            }
        }


    }
}
