using System;
using System.Text;
using System.Text.Json;
using Kavita.Models.DTOs.Kobo;

namespace Kavita.Services.Helpers.Kobo;

public static class KoboSyncTokenHelper
{
    private const string TokenPrefix = "kavita.";

    public static KoboSyncToken Decode(string? token)
    {
        if (string.IsNullOrEmpty(token))
        {
            return new KoboSyncToken();
        }

        try
        {
            if (token.StartsWith(TokenPrefix))
            {
                var decodedBase64 = Encoding.UTF8.GetString(Convert.FromBase64String(token[TokenPrefix.Length..]));
                return JsonSerializer.Deserialize<KoboSyncToken>(decodedBase64) ?? new KoboSyncToken();
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
                        return new KoboSyncToken { KoboStoreSyncToken = koboStoreSyncToken, };
                    }
                }
                catch
                {
                    /* Swallow */
                }
            }

            return new KoboSyncToken { KoboStoreSyncToken = token };
        }
        catch
        {
            /* Swallow */
        }

        return new KoboSyncToken();
    }

    public static string Encode(KoboSyncToken token)
    {
        var s = JsonSerializer.Serialize(token);
        return TokenPrefix + Convert.ToBase64String(Encoding.UTF8.GetBytes(s));
    }

}
