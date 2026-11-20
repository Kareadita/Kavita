using Flurl.Http.Testing;
using Kavita.Common.Helpers;

namespace Kavita.Common.Tests.Helpers;

public class FlurlConfigurationTests
{
    [Fact]
    public async Task DownloadToFileAsync_OverCap_ThrowsAndLeavesNoFile()
    {
        using var httpTest = new HttpTest();
        httpTest.RespondWith(() => new ByteArrayContent(new byte[4096]));
        var path = Path.Join(Path.GetTempPath(), $"kavita-cap-{Guid.NewGuid():N}.bin");

        await Assert.ThrowsAsync<KavitaException>(() =>
            FlurlConfiguration.DownloadToFileAsync("https://example.com/cover.png", path, maxBytes: 1024));

        Assert.False(File.Exists(path));
    }
}
