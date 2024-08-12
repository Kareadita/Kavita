import {Person} from "../metadata/person";

export interface IHasCast {
  writerLocked: boolean;
  coverArtistLocked: boolean;
  publisherLocked: boolean;
  characterLocked: boolean;
  pencillerLocked: boolean;
  inkerLocked: boolean;
  imprintLocked: boolean;
  coloristLocked: boolean;
  lettererLocked: boolean;
  editorLocked: boolean;
  translatorLocked: boolean;
  teamLocked: boolean;
  locationLocked: boolean;
  languageLocked: boolean;

  writers: Array<Person>;
  coverArtists: Array<Person>;
  publishers: Array<Person>;
  characters: Array<Person>;
  pencillers: Array<Person>;
  inkers: Array<Person>;
  imprints: Array<Person>;
  colorists: Array<Person>;
  letterers: Array<Person>;
  editors: Array<Person>;
  translators: Array<Person>;
  teams: Array<Person>;
  locations: Array<Person>;
}
