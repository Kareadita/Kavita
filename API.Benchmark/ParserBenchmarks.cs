﻿using System;
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

        private static string Normalize(string name)
        {
            // ReSharper disable once UnusedVariable
            var ret = NormalizeRegex.Replace(name, string.Empty).ToLower();
            var normalized = NormalizeRegex.Replace(name, string.Empty).ToLower();
            return string.IsNullOrEmpty(normalized) ? name : normalized;
        }



        [Benchmark]
        public void TestNormalizeName()
        {
            foreach (var name in _names)
            {
                Normalize(name);
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
