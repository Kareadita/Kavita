import {IHasCover} from "../common/i-has-cover";

export enum PersonRole {
  Other = 1,
  Writer = 3,
  Penciller = 4,
  Inker = 5,
  Colorist = 6,
  Letterer = 7,
  CoverArtist = 8,
  Editor = 9,
  Publisher = 10,
  Character = 11,
  Translator = 12,
  Imprint = 13,
  Team = 14,
  Location = 15
}

export interface Person extends IHasCover {
  id: number;
  name: string;
  description: string;
  aliases: Array<string>;
  coverImage?: string;
  coverImageLocked: boolean;
  malId?: number;
  aniListId?: number;
  hardcoverId?: string;
  asin?: string;
  primaryColor: string;
  secondaryColor: string;
  /**
   * Only present when retrieving from person info endpoint
   */
  webLinks?: string[];
  /**
   * Only present when retrieving from person info endpoint
   */
  roles?: PersonRole[];
}

/**
 * Excludes Other as it's not in use
 */
export const allPeopleRoles = [
  PersonRole.Writer,
  PersonRole.Penciller,
  PersonRole.Inker,
  PersonRole.Colorist,
  PersonRole.Letterer,
  PersonRole.CoverArtist,
  PersonRole.Editor,
  PersonRole.Publisher,
  PersonRole.Character,
  PersonRole.Translator,
  PersonRole.Imprint,
  PersonRole.Team,
  PersonRole.Location
]
