using System;
using System.Text;
using System.Text.Json;
using Kavita.Models.DTOs.Kobo;

namespace Kavita.Services.Helpers.Kobo;

public static class KoboSyncTokenHelper
{
    internal const string TokenPrefix = "kavita.";

    public static KoboSyncTokenDecodeResult Decode(string? token)
    {
        if (string.IsNullOrEmpty(token))
        {
            return new KoboSyncTokenDecodeResult();
        }

        try
        {
            if (token.StartsWith(TokenPrefix))
            {
                var decodedBase64 = Encoding.UTF8.GetString(Convert.FromBase64String(token[TokenPrefix.Length..]));
                var legacy = JsonSerializer.Deserialize<LegacyToken>(decodedBase64);

                return new KoboSyncTokenDecodeResult
                {
                    Token = new KoboSyncToken
                    {
                        Version = legacy?.Version ?? 1,
                        SyncPointId = legacy?.SyncPointId,
                    },
                    KoboStoreSyncToken = legacy?.KoboStoreSyncToken,
                };
            }

            // CalibreWeb token
            if (!token.Contains('.'))
            {
                try
                {
                    var json = JsonSerializer.Deserialize<JsonElement>(Convert.FromBase64String(token));
                    var koboStoreSyncToken = json
                        .GetProperty("data")
                        .GetProperty("raw_kobo_store_token")
                        .GetString();

                    if (!string.IsNullOrEmpty(koboStoreSyncToken))
                    {
                        return new KoboSyncTokenDecodeResult { KoboStoreSyncToken = koboStoreSyncToken, };
                    }
                }
                catch
                {
                    /* Swallow */
                }
            }

            return new KoboSyncTokenDecodeResult { KoboStoreSyncToken = token };
        }
        catch
        {
            /* Swallow */
        }

        return new KoboSyncTokenDecodeResult();
    }

    public static string Encode(int syncPointId)
    {
        var s = JsonSerializer.Serialize(new KoboSyncToken { SyncPointId = syncPointId });
        return TokenPrefix + Convert.ToBase64String(Encoding.UTF8.GetBytes(s));
    }

    /// <summary>
    /// Token from before header fix. See https://github.com/Kareadita/Kavita/issues/5008#issuecomment-6728032994
    /// </summary>
    private sealed record LegacyToken
    {
        public int Version { get; set; } = 1;
        public string? KoboStoreSyncToken { get; set; }
        public int? SyncPointId { get; set; }
    }

}
