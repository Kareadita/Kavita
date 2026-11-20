using System.Text;
using System.Text.Json;
using Kavita.Services.Helpers.Kobo;

namespace Kavita.Services.Tests.Kobo;

public class KoboSyncTokenHelperTests
{

    [Fact]
    public void Decode_BackwardsCompat()
    {
        var token = LegacyToken("upstream-token", 12);

        var decoded = KoboSyncTokenHelper.Decode(token);

        Assert.Equal(12, decoded.Token.SyncPointId);
        Assert.Equal("upstream-token", decoded.KoboStoreSyncToken);
    }

    private static string LegacyToken(string? storeToken, int? syncPointId)
    {
        var json = JsonSerializer.Serialize(new
        {
            Version = 1,
            KoboStoreSyncToken = storeToken,
            SyncPointId = syncPointId,
        });
        return KoboSyncTokenHelper.TokenPrefix + Convert.ToBase64String(Encoding.UTF8.GetBytes(json));
    }
}
