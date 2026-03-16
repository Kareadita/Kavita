using Kavita.Server.ManualMigrations.v0._7._11;

namespace Kavita.Server.Tests.ManualMigrations;

public class MigrateSmartFilterEncodingTests
{

    [Theory]
    [InlineData("", false)]
    [InlineData("name=DC%20-%20On%20Deck&stmts=comparison%3D1%26field%3D20%26value%3D0,comparison%3D9%26field%3D20%26value%3D100,comparison%3D0%26field%3D19%26value%3D274&sortOptions=sortField%3D1&isAscending=True&limitTo=0&combination=1", true)]
    [InlineData("name=English%20In%20Progress&stmts=comparison%253D8%252Cfield%253D7%252Cvalue%253D4%25252C3,comparison%253D3%252Cfield%253D20%252Cvalue%253D100,comparison%253D8%252Cfield%253D3%252Cvalue%253Dja,comparison%253D1%252Cfield%253D20%252Cvalue%253D0&sortOptions=sortField%3D7,isAscending%3DFalse&limitTo=0&combination=1", true)]
    [InlineData("name=Unread%20Isekai%20Light%20Novels&stmts=comparison%253D0%25C2%25A6field%253D20%25C2%25A6value%253D0%EF%BF%BDcomparison%253D5%25C2%25A6field%253D6%25C2%25A6value%253D230%EF%BF%BDcomparison%253D8%25C2%25A6field%253D7%25C2%25A6value%253D4%EF%BF%BDcomparison%253D0%25C2%25A6field%253D19%25C2%25A6value%253D14&sortOptions=sortField%3D5%C2%A6isAscending%3DFalse&limitTo=0&combination=1", false)]
    [InlineData("name=Zero&stmts=comparison%3d7%26field%3d1%26value%3d0&sortOptions=sortField=2&isAscending=False&limitTo=0&combination=1", true)]
    public void Test_ShouldMigrateFilter(string filter, bool expected)
    {
        Assert.Equal(expected, MigrateSmartFilterEncoding.ShouldMigrateFilter(filter));
    }

    [Theory]
    [InlineData("name=DC%20-%20On%20Deck&stmts=comparison%3D1%26field%3D20%26value%3D0,comparison%3D9%26field%3D20%26value%3D100,comparison%3D0%26field%3D19%26value%3D274&sortOptions=sortField%3D1&isAscending=True&limitTo=0&combination=1")]
    [InlineData("name=Manga%20-%20On%20Deck&stmts=comparison%253D1%252Cfield%253D20%252Cvalue%253D0,comparison%253D3%252Cfield%253D20%252Cvalue%253D100,comparison%253D0%252Cfield%253D19%252Cvalue%253D2&sortOptions=sortField%3D1,isAscending%3DTrue&limitTo=0&combination=1")]
    [InlineData("name=English%20In%20Progress&stmts=comparison%253D8%252Cfield%253D7%252Cvalue%253D4%25252C3,comparison%253D3%252Cfield%253D20%252Cvalue%253D100,comparison%253D8%252Cfield%253D3%252Cvalue%253Dja,comparison%253D1%252Cfield%253D20%252Cvalue%253D0&sortOptions=sortField%3D7,isAscending%3DFalse&limitTo=0&combination=1")]
    public void MigrationWorks(string filter)
    {
        try
        {
            var updatedFilter = MigrateSmartFilterEncoding.EncodeFix(filter);
            Assert.NotNull(updatedFilter);
        }
        catch (Exception ex)
        {
            Assert.Fail("Exception thrown: " + ex.Message);
        }

    }
}
