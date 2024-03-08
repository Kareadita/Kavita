using API.Services;
using API.Services.Tasks.Scanner.Parser;
using NSubstitute;

namespace API.Tests.Parsers;

public class ImageParserTests
{
    private readonly ImageParser _parser;
    public ImageParserTests()
    {
        var ds = Substitute.For<IDirectoryService>();
        _parser = new ImageParser(ds);
    }

    #region Parse



    #endregion

    #region IsApplicable



    #endregion
}
