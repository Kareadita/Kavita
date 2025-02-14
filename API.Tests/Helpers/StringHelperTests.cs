using System;
using API.Helpers;
using Xunit;

namespace API.Tests.Helpers;

public class StringHelperTests
{
    [Theory]
    [InlineData(
        "<p>A Perfect Marriage Becomes a Perfect Affair!<br /> <br><br><br /> Every woman wishes for that happily ever after, but when time flies by and you've become a neglected housewife, what's a woman to do?</p>",
        "<p>A Perfect Marriage Becomes a Perfect Affair!<br /> Every woman wishes for that happily ever after, but when time flies by and you've become a neglected housewife, what's a woman to do?</p>"
    )]
    public void Test(string input, string expected)
    {
        Assert.Equal(expected, StringHelper.SquashBreaklines(input));
    }
}
