using System;
using System.Linq;
using System.Threading.Tasks;
using API.Services;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Order;
using HtmlAgilityPack;
using VersOne.Epub;

namespace API.Benchmark;

[MemoryDiagnoser]
[Orderer(SummaryOrderPolicy.FastestToSlowest)]
[RankColumn]
[SimpleJob(launchCount: 1, warmupCount: 3, targetCount: 5, invocationCount: 100, id: "Epub"), ShortRunJob]
public class EpubBenchmark
{
    [Benchmark]
    public async Task GetWordCount_PassByString()
    {
        using var book = await EpubReader.OpenBookAsync("Data/book-test.epub", BookService.BookReaderOptions);
        foreach (var bookFile in book.Content.Html.Values)
        {
            Console.WriteLine(GetBookWordCount_PassByString(await bookFile.ReadContentAsTextAsync()));
            ;
        }
    }

    [Benchmark]
    public async Task GetWordCount_PassByRef()
    {
        using var book = await EpubReader.OpenBookAsync("Data/book-test.epub", BookService.BookReaderOptions);
        foreach (var bookFile in book.Content.Html.Values)
        {
            Console.WriteLine(await GetBookWordCount_PassByRef(bookFile));
        }
    }

    private static int GetBookWordCount_PassByString(string fileContents)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(fileContents);
        var delimiter = new char[] {' '};

        return doc.DocumentNode.SelectNodes("//body//text()[not(parent::script)]")
            .Select(node => node.InnerText)
            .Select(text => text.Split(delimiter, StringSplitOptions.RemoveEmptyEntries)
                .Where(s => char.IsLetter(s[0])))
            .Select(words => words.Count())
            .Where(wordCount => wordCount > 0)
            .Sum();
    }

    private static async Task<int> GetBookWordCount_PassByRef(EpubContentFileRef bookFile)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(await bookFile.ReadContentAsTextAsync());
        var delimiter = new char[] {' '};

        return doc.DocumentNode.SelectNodes("//body//text()[not(parent::script)]")
            .Select(node => node.InnerText)
            .Select(text => text.Split(delimiter, StringSplitOptions.RemoveEmptyEntries)
                .Where(s => char.IsLetter(s[0])))
            .Select(words => words.Count())
            .Where(wordCount => wordCount > 0)
            .Sum();
    }
}
