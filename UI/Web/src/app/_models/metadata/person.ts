import {IHasCover} from "../common/i-has-cover";

export enum PersonRole {
  Other = 1,
  Artist = 2,
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
  coverImage?: string;
  coverImageLocked: boolean;
  malId?: number;
  aniListId?: number;
  hardcoverId?: string;
  asin?: string;
  primaryColor: string;
  secondaryColor: string;
}
