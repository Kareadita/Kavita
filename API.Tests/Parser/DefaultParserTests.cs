using System.IO.Abstractions.TestingHelpers;
using API.Parser;
using API.Services;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace API.Tests.Parser;

public class DefaultParserTests
{
    private readonly DefaultParser _defaultParser;
    public DefaultParserTests()
    {
        var directoryService = new DirectoryService(Substitute.For<ILogger<DirectoryService>>(), new MockFileSystem());
        _defaultParser = new DefaultParser(directoryService);
    }

    [Theory]
    [InlineData("C:/", "C:/Love Hina/Love Hina - Special.cbz", "Love Hina")]
    [InlineData("C:/", "C:/Love Hina/Specials/Ani-Hina Art Collection.cbz", "Love Hina")]
    [InlineData("C:/", "C:/Mujaki no Rakuen Something/Mujaki no Rakuen Vol12 ch76.cbz", "Mujaki no Rakuen")]
    public void FallbackTest(string rootDir, string inputPath, string expectedSeries)
    {
        var actual = _defaultParser.Parse(inputPath, rootDir);
        if (actual == null)
        {
            Assert.NotNull(actual);
            return;
        }

        Assert.Equal(expectedSeries, actual.Series);
    }
}
