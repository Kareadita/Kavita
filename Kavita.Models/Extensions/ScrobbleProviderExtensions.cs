using System;
using Kavita.Models.DTOs.KavitaPlus.OAuth;
using Kavita.Models.Entities.Enums;

namespace Kavita.Models.Extensions;

public static class ScrobbleProviderExtensions
{

    public static OAuthUpstream? ToOAuthUpstream(this ScrobbleProvider scrobbleProvider) => scrobbleProvider switch
    {
        ScrobbleProvider.Kavita => null,
        ScrobbleProvider.AniList => OAuthUpstream.AniList,
        ScrobbleProvider.Mal => OAuthUpstream.MyAnimeList,
        ScrobbleProvider.Cbr => null,
        ScrobbleProvider.Hardcover => null,
        ScrobbleProvider.MangaBaka => OAuthUpstream.MangaBaka,
        _ => throw new ArgumentOutOfRangeException(nameof(scrobbleProvider), scrobbleProvider, null)
    };

}
