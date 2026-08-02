using System;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Kavita.Services.Kobo;

/// <summary>
/// Calibre-Web-shaped <c>x-kobo-synctoken</c> watermark (base64 JSON).
/// </summary>
public sealed class KoboSyncToken
{
    public const string HeaderName = "x-kobo-synctoken";
    public const string ContinueHeaderName = "x-kobo-sync";
    public const string ContinueHeaderValue = "continue";
    public const string Version = "1-1-0";

    public string RawKoboStoreToken { get; set; } = string.Empty;
    public DateTime BooksLastCreated { get; set; } = DateTime.MinValue;
    public DateTime BooksLastModified { get; set; } = DateTime.MinValue;
    public DateTime ArchiveLastModified { get; set; } = DateTime.MinValue;
    public DateTime ReadingStateLastModified { get; set; } = DateTime.MinValue;
    public DateTime TagsLastModified { get; set; } = DateTime.MinValue;

    public static KoboSyncToken FromHeader(string? headerValue)
    {
        if (string.IsNullOrWhiteSpace(headerValue))
        {
            return new KoboSyncToken();
        }

        // Official Kobo store tokens contain a '.' — treat as opaque store token.
        if (headerValue.Contains('.', StringComparison.Ordinal))
        {
            return new KoboSyncToken { RawKoboStoreToken = headerValue };
        }

        try
        {
            var padded = headerValue + new string('=', (4 - headerValue.Length % 4) % 4);
            var json = Encoding.UTF8.GetString(Convert.FromBase64String(padded));
            var envelope = JsonSerializer.Deserialize<TokenEnvelope>(json);
            if (envelope?.Data == null)
            {
                return new KoboSyncToken();
            }

            return new KoboSyncToken
            {
                RawKoboStoreToken = envelope.Data.RawKoboStoreToken ?? string.Empty,
                BooksLastModified = FromEpoch(envelope.Data.BooksLastModified),
                BooksLastCreated = FromEpoch(envelope.Data.BooksLastCreated),
                ArchiveLastModified = FromEpoch(envelope.Data.ArchiveLastModified),
                ReadingStateLastModified = FromEpoch(envelope.Data.ReadingStateLastModified),
                TagsLastModified = FromEpoch(envelope.Data.TagsLastModified),
            };
        }
        catch
        {
            return new KoboSyncToken();
        }
    }

    public string ToHeaderValue()
    {
        var envelope = new TokenEnvelope
        {
            Version = Version,
            Data = new TokenData
            {
                RawKoboStoreToken = RawKoboStoreToken,
                BooksLastModified = ToEpoch(BooksLastModified),
                BooksLastCreated = ToEpoch(BooksLastCreated),
                ArchiveLastModified = ToEpoch(ArchiveLastModified),
                ReadingStateLastModified = ToEpoch(ReadingStateLastModified),
                TagsLastModified = ToEpoch(TagsLastModified),
            },
        };

        var json = JsonSerializer.Serialize(envelope);
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(json));
    }

    private static DateTime FromEpoch(double? epoch)
    {
        if (epoch == null) return DateTime.MinValue;
        try
        {
            return DateTime.UnixEpoch.AddSeconds(epoch.Value);
        }
        catch
        {
            return DateTime.MinValue;
        }
    }

    private static double ToEpoch(DateTime value)
    {
        if (value == DateTime.MinValue) return 0;
        return (value.ToUniversalTime() - DateTime.UnixEpoch).TotalSeconds;
    }

    private sealed class TokenEnvelope
    {
        [JsonPropertyName("version")]
        public string Version { get; set; } = "1-1-0";

        [JsonPropertyName("data")]
        public TokenData? Data { get; set; }
    }

    private sealed class TokenData
    {
        [JsonPropertyName("raw_kobo_store_token")]
        public string? RawKoboStoreToken { get; set; }

        [JsonPropertyName("books_last_modified")]
        public double? BooksLastModified { get; set; }

        [JsonPropertyName("books_last_created")]
        public double? BooksLastCreated { get; set; }

        [JsonPropertyName("archive_last_modified")]
        public double? ArchiveLastModified { get; set; }

        [JsonPropertyName("reading_state_last_modified")]
        public double? ReadingStateLastModified { get; set; }

        [JsonPropertyName("tags_last_modified")]
        public double? TagsLastModified { get; set; }
    }
}
