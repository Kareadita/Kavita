import {ScrobbleProvider} from "../_services/scrobbling.service";
import {AgeRating} from "./metadata/age-rating";

// Deprecated in v0.8, replaced with UserCollection
// export interface CollectionTag {
//     id: number;
//     title: string;
//     promoted: boolean;
//     /**
//      * This is used as a placeholder to store the coverImage url. The backend does not use this or send it.
//      */
//     coverImage: string;
//     coverImageLocked: boolean;
//     summary: string;
// }

export interface UserCollection {
  id: number;
  title: string;
  promoted: boolean;
  /**
   * This is used as a placeholder to store the coverImage url. The backend does not use this or send it.
   */
  coverImage: string;
  coverImageLocked: boolean;
  summary: string;
  lastSyncUtc: string;
  owner: string;
  source: ScrobbleProvider;
  sourceUrl: string | null;
  ageRating: AgeRating;

}
