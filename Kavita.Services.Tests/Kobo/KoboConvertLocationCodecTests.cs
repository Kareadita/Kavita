using Kavita.Services.Kobo;
using Xunit;

namespace Kavita.Services.Tests.Kobo;

public class KoboConvertLocationCodecTests
{
    [Fact]
    public void TryEncode_Omits_WhenReadyToRead()
    {
        Assert.Null(KoboConvertLocationCodec.TryEncode(0, 10, readyToRead: true));
        Assert.Null(KoboConvertLocationCodec.TryEncode(5, 10, readyToRead: true));
        Assert.Null(KoboConvertLocationCodec.TryEncode(1, 0));
        Assert.Null(KoboConvertLocationCodec.TryEncode(1, -5));
        Assert.Null(KoboConvertLocationCodec.TryEncode(-1, 10));
    }

    [Fact]
    public void TryEncode_MapsInProgressPagesRead_ToPageDocument()
    {
        var mapped = KoboConvertLocationCodec.TryEncode(0, 10);
        Assert.NotNull(mapped);
        Assert.Equal(KoboConvertLocationCodec.ValueKoboSpan, mapped.Value);
        Assert.Equal(KoboConvertLocationCodec.TypeKoboSpan, mapped.Type);
        Assert.Equal("OEBPS/Text/page_0001.xhtml", mapped.Source);

        mapped = KoboConvertLocationCodec.TryEncode(1, 10);
        Assert.Equal("OEBPS/Text/page_0002.xhtml", mapped!.Source);

        mapped = KoboConvertLocationCodec.TryEncode(4, 10);
        Assert.Equal("OEBPS/Text/page_0005.xhtml", mapped!.Source);
    }

    [Fact]
    public void TryEncode_MapsFinished_ToLastPageDocument()
    {
        var mapped = KoboConvertLocationCodec.TryEncode(10, 10);
        Assert.NotNull(mapped);
        Assert.Equal("OEBPS/Text/page_0010.xhtml", mapped.Source);
        Assert.Equal(KoboConvertLocationCodec.ValueKoboSpan, mapped.Value);

        mapped = KoboConvertLocationCodec.TryEncode(12, 10);
        Assert.Equal("OEBPS/Text/page_0010.xhtml", mapped!.Source);
    }

    [Fact]
    public void TryDecode_RequiresKoboSpanAndKobo11()
    {
        Assert.False(KoboConvertLocationCodec.TryDecode("kobo.1.1", "Other", "OEBPS/Text/page_0003.xhtml",
            10, out _));
        Assert.False(KoboConvertLocationCodec.TryDecode("kobo.2.1", "KoboSpan", "OEBPS/Text/page_0003.xhtml",
            10, out _));
        Assert.False(KoboConvertLocationCodec.TryDecode(null, "KoboSpan", "OEBPS/Text/page_0003.xhtml",
            10, out _));
    }

    [Theory]
    [InlineData("OEBPS/Text/page_0003.xhtml", 2)]
    [InlineData("Text/page_0003.xhtml", 2)]
    [InlineData("page_0003.xhtml", 2)]
    [InlineData(@"OEBPS\Text\page_0003.xhtml", 2)]
    [InlineData("OEBPS/Text/page_0001.xhtml", 0)]
    [InlineData("OEBPS/Text/page_0010.xhtml", 10)]
    public void TryDecode_AcceptsFullPathOrSuffixOrBasename(string source, int expectedPagesRead)
    {
        Assert.True(KoboConvertLocationCodec.TryDecode(
            KoboConvertLocationCodec.ValueKoboSpan,
            KoboConvertLocationCodec.TypeKoboSpan,
            source,
            10,
            out var pagesRead));
        Assert.Equal(expectedPagesRead, pagesRead);
    }

    [Fact]
    public void TryDecode_MapsLastPageDocument_ToFinishedPagesRead()
    {
        Assert.True(KoboConvertLocationCodec.TryDecode(
            KoboConvertLocationCodec.ValueKoboSpan,
            KoboConvertLocationCodec.TypeKoboSpan,
            "OEBPS/Text/page_0010.xhtml",
            10,
            out var pagesRead));
        Assert.Equal(10, pagesRead);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("OEBPS/Text/chapter1.xhtml")]
    [InlineData("OEBPS/Text/page_abc.xhtml")]
    [InlineData("OEBPS/Text/page_3.xhtml")]
    [InlineData("OEBPS/Text/page_0000.xhtml")]
    [InlineData("OEBPS/Text/page_0011.xhtml")] // beyond totalPages
    public void TryDecode_FailsClosed_OnBadSourceOrRange(string? source)
    {
        Assert.False(KoboConvertLocationCodec.TryDecode(
            KoboConvertLocationCodec.ValueKoboSpan,
            KoboConvertLocationCodec.TypeKoboSpan,
            source,
            10,
            out var pagesRead));
        Assert.Equal(0, pagesRead);
    }

    [Fact]
    public void TryDecode_FailsClosed_WhenTotalPagesInvalid()
    {
        Assert.False(KoboConvertLocationCodec.TryDecode(
            KoboConvertLocationCodec.ValueKoboSpan,
            KoboConvertLocationCodec.TypeKoboSpan,
            "OEBPS/Text/page_0001.xhtml",
            0,
            out _));
    }

    [Fact]
    public void EncodeDecode_RoundTripsInProgressPages()
    {
        // Last in-progress index (totalPages-1) shares a Source with finished and decodes as finished.
        for (var pagesRead = 0; pagesRead < 9; pagesRead++)
        {
            var encoded = KoboConvertLocationCodec.TryEncode(pagesRead, 10);
            Assert.NotNull(encoded);
            Assert.True(KoboConvertLocationCodec.TryDecode(encoded.Value, encoded.Type, encoded.Source, 10,
                out var decoded));
            Assert.Equal(pagesRead, decoded);
        }
    }

    [Fact]
    public void EncodeDecode_RoundTripsFinished()
    {
        var encoded = KoboConvertLocationCodec.TryEncode(10, 10);
        Assert.NotNull(encoded);
        Assert.True(KoboConvertLocationCodec.TryDecode(encoded.Value, encoded.Type, encoded.Source, 10,
            out var decoded));
        Assert.Equal(10, decoded);

        encoded = KoboConvertLocationCodec.TryEncode(15, 10);
        Assert.NotNull(encoded);
        Assert.True(KoboConvertLocationCodec.TryDecode(encoded.Value, encoded.Type, encoded.Source, 10,
            out decoded));
        Assert.Equal(10, decoded);
    }
}
