namespace Kavita.Services.Tests.ReadingLists;

public class CblParserTests
{
    private readonly string _testDirectory = Path.Join(Directory.GetCurrentDirectory(), "../../../Test Data/CblParserTests/Test Cases");

    #region V1 Spec

    [Fact]
    public void ParseV1Test_NoSpecial()
    {
        const string filename = "[DC Comics] Aquaman- Death of a Prince (WEB-CBRO).cbl";
        Assert.True(false);
    }

    [Fact]
    public void ParseV1Test_Special()
    {
        const string filename = "BOOM! Power Rangers Simplified 1a.cbl";
        Assert.True(false);
    }

    #endregion


    #region V2 Spec

    [Fact]
    public void ParseV2Test()
    {
        const string filename = "2018-2021 Part 16.1 Reborn Again.json";
        Assert.True(false);
    }
    #endregion
}
