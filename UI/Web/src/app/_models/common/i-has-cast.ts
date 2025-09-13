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

export function hasAnyCast(entity: IHasCast | null | undefined): boolean {
  if (entity === null || entity === undefined) return false;

  return entity.writers.length > 0 ||
    entity.coverArtists.length > 0 ||
    entity.publishers.length > 0 ||
    entity.characters.length > 0 ||
    entity.pencillers.length > 0 ||
    entity.inkers.length > 0 ||
    entity.imprints.length > 0 ||
    entity.colorists.length > 0 ||
    entity.letterers.length > 0 ||
    entity.editors.length > 0 ||
    entity.translators.length > 0 ||
    entity.teams.length > 0 ||
    entity.locations.length > 0;
}
