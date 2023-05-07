using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using API.Services;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Order;
using HtmlAgilityPack;
using VersOne.Epub;

namespace API.Benchmark;

[StopOnFirstError]
[MemoryDiagnoser]
[RankColumn]
[Orderer(SummaryOrderPolicy.FastestToSlowest)]
[SimpleJob(launchCount: 1, warmupCount: 5, invocationCount: 20)]
public class EpubBenchmark
{
    private const string FilePath = @"E:\Books\Invaders of the Rokujouma\Invaders of the Rokujouma - Volume 01.epub";
    private readonly Regex _wordRegex = new Regex(@"\b\w+\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    [Benchmark]
    public async Task GetWordCount_PassByRef()
    {
        using var book = await EpubReader.OpenBookAsync(FilePath, BookService.BookReaderOptions);
        foreach (var bookFile in book.Content.Html.Values)
        {
            await GetBookWordCount_PassByRef(bookFile);
        }
    }

    [Benchmark]
    public async Task GetBookWordCount_SumEarlier()
    {
        using var book = await EpubReader.OpenBookAsync(FilePath, BookService.BookReaderOptions);
        foreach (var bookFile in book.Content.Html.Values)
        {
            await GetBookWordCount_SumEarlier(bookFile);
        }
    }

    [Benchmark]
    public async Task GetBookWordCount_Regex()
    {
        using var book = await EpubReader.OpenBookAsync(FilePath, BookService.BookReaderOptions);
        foreach (var bookFile in book.Content.Html.Values)
        {
            await GetBookWordCount_Regex(bookFile);
        }
    }

    private int GetBookWordCount_PassByString(string fileContents)
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

    private async Task<int> GetBookWordCount_PassByRef(EpubContentFileRef bookFile)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(await bookFile.ReadContentAsTextAsync());
        var delimiter = new char[] {' '};

        var textNodes = doc.DocumentNode.SelectNodes("//body//text()[not(parent::script)]");
        if (textNodes == null) return 0;
        return textNodes.Select(node => node.InnerText)
            .Select(text => text.Split(delimiter, StringSplitOptions.RemoveEmptyEntries)
                .Where(s => char.IsLetter(s[0])))
            .Select(words => words.Count())
            .Where(wordCount => wordCount > 0)
            .Sum();
    }

    private async Task<int> GetBookWordCount_SumEarlier(EpubContentFileRef bookFile)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(await bookFile.ReadContentAsTextAsync());

        return doc.DocumentNode.SelectNodes("//body//text()[not(parent::script)]")
            .DefaultIfEmpty()
            .Select(node => node.InnerText.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Where(s => char.IsLetter(s[0])))
            .Sum(words => words.Count());
    }

    private async Task<int> GetBookWordCount_Regex(EpubContentFileRef bookFile)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(await bookFile.ReadContentAsTextAsync());


        return doc.DocumentNode.SelectNodes("//body//text()[not(parent::script)]")
            .Sum(node => _wordRegex.Matches(node.InnerText).Count);
    }
}
