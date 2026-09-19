import {IHasCast} from "../_models/common/i-has-cast";
import {Person, PersonRole} from "../_models/metadata/person";

export type PersonLockKey = Exclude<Extract<keyof IHasCast, `${string}Locked`>, 'languageLocked'>;
export type PersonModelKey = Exclude<keyof IHasCast, `${string}Locked`>;
export type PersonFields = Record<PersonModelKey, Array<Person>>;

/** id is the typeahead's DOM id, referenced by its label, so it must stay as-is */
export const personFields: Record<PersonRole, {id: string; model: PersonModelKey; lock: PersonLockKey}> = {
  [PersonRole.Writer]: {id: 'writer', model: 'writers', lock: 'writerLocked'},
  [PersonRole.Penciller]: {id: 'penciller', model: 'pencillers', lock: 'pencillerLocked'},
  [PersonRole.Inker]: {id: 'inker', model: 'inkers', lock: 'inkerLocked'},
  [PersonRole.Colorist]: {id: 'colorist', model: 'colorists', lock: 'coloristLocked'},
  [PersonRole.Letterer]: {id: 'letterer', model: 'letterers', lock: 'lettererLocked'},
  [PersonRole.CoverArtist]: {id: 'cover-artist', model: 'coverArtists', lock: 'coverArtistLocked'},
  [PersonRole.Editor]: {id: 'editor', model: 'editors', lock: 'editorLocked'},
  [PersonRole.Publisher]: {id: 'publisher', model: 'publishers', lock: 'publisherLocked'},
  [PersonRole.Character]: {id: 'character', model: 'characters', lock: 'characterLocked'},
  [PersonRole.Translator]: {id: 'translator', model: 'translators', lock: 'translatorLocked'},
  [PersonRole.Imprint]: {id: 'imprint', model: 'imprints', lock: 'imprintLocked'},
  [PersonRole.Team]: {id: 'teams', model: 'teams', lock: 'teamLocked'},
  [PersonRole.Location]: {id: 'locations', model: 'locations', lock: 'locationLocked'},
};

export function personFieldsFrom(entity: Partial<IHasCast>): PersonFields {
  return Object.values(personFields).reduce((acc, field) => {
    acc[field.model] = entity[field.model] ?? [];
    return acc;
  }, {} as PersonFields);
}
