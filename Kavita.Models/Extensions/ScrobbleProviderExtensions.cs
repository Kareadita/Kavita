using System;
using Kavita.Models.DTOs.KavitaPlus.OAuth;
using Kavita.Models.Entities.Enums;

namespace Kavita.Models.Extensions;

public static class ScrobbleProviderExtensions
{

    extension(ScrobbleProvider scrobbleProvider)
    {
        public OAuthUpstream? ToOAuthUpstream() => scrobbleProvider switch
        {
            ScrobbleProvider.Kavita => null,
            ScrobbleProvider.AniList => OAuthUpstream.AniList,
            ScrobbleProvider.Mal => OAuthUpstream.MyAnimeList,
            ScrobbleProvider.Cbr => null,
            ScrobbleProvider.Hardcover => null,
            ScrobbleProvider.MangaBaka => OAuthUpstream.MangaBaka,
            _ => throw new ArgumentOutOfRangeException(nameof(scrobbleProvider), scrobbleProvider, null)
        };

        public bool SupportsOAuthTokenRefresh() => scrobbleProvider switch
        {
            ScrobbleProvider.Mal or ScrobbleProvider.MangaBaka => true,
            _ => false
        };
    }
}
