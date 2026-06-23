import {ScrobbleProvider} from "../../_services/scrobbling.service";

export function getProviderUrl(provider: ScrobbleProvider, id: number, isStandAlone: boolean = false): string | null {
  switch (provider) {
    case ScrobbleProvider.AniList:   return `https://anilist.co/manga/${id}/`;
    case ScrobbleProvider.Mal:       return `https://myanimelist.net/manga/${id}/`;
    case ScrobbleProvider.MangaBaka: return `https://mangabaka.org/${id}`;
    case ScrobbleProvider.Cbr:       return `https://comicbookroundup.com/comic-books/reviews/${id}`;
    case ScrobbleProvider.Hardcover: return isStandAlone ? `https://hardcover.app/id/book/${id}` : `https://hardcover.app/id/series/${id}`;
    default: return null;
  }
}
