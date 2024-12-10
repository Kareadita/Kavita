import {PersonRole} from "../person";

export enum FilterField
{
    None = -1,
    Summary = 0,
    SeriesName = 1,
    PublicationStatus = 2,
    Languages = 3,
    AgeRating = 4,
    UserRating = 5,
    Tags = 6,
    CollectionTags = 7,
    Translators = 8,
    Characters = 9,
    Publisher = 10,
    Editor = 11,
    CoverArtist = 12,
    Letterer = 13,
    Colorist = 14,
    Inker = 15,
    Penciller = 16,
    Writers = 17,
    Genres = 18,
    Libraries = 19,
    ReadProgress = 20,
    Formats = 21,
    ReleaseYear = 22,
    ReadTime = 23,
    Path = 24,
    FilePath = 25,
    WantToRead = 26,
    ReadingDate = 27,
    AverageRating = 28,
    Imprint = 29,
    Team = 30,
    Location = 31,
    ReadLast = 32
}


const enumArray = Object.keys(FilterField)
    .filter(key => !isNaN(Number(key)) && parseInt(key, 10) >= 0)
    .map(key => {
        // @ts-ignore
        return ({key: key, value: FilterField[key]});
    });

enumArray.sort((a, b) => a.value.localeCompare(b.value));

export const allFields = enumArray
    .map(key => parseInt(key.key, 10))as FilterField[];

export const allPeople = [
  FilterField.Characters,
  FilterField.Colorist,
  FilterField.CoverArtist,
  FilterField.Editor,
  FilterField.Inker,
  FilterField.Letterer,
  FilterField.Penciller,
  FilterField.Publisher,
  FilterField.Translators,
  FilterField.Writers,
];

export const personRoleForFilterField = (role: PersonRole) => {
  switch (role) {
    case PersonRole.Artist: return FilterField.CoverArtist;
    case PersonRole.Character: return FilterField.Characters;
    case PersonRole.Colorist: return FilterField.Colorist;
    case PersonRole.CoverArtist: return FilterField.CoverArtist;
    case PersonRole.Editor: return FilterField.Editor;
    case PersonRole.Inker: return FilterField.Inker;
    case PersonRole.Letterer: return FilterField.Letterer;
    case PersonRole.Penciller: return FilterField.Penciller;
    case PersonRole.Publisher: return FilterField.Publisher;
    case PersonRole.Translator: return FilterField.Translators;
    case PersonRole.Writer: return FilterField.Writers;
    case PersonRole.Imprint: return FilterField.Imprint;
    case PersonRole.Location: return FilterField.Location;
    case PersonRole.Team: return FilterField.Team;
    case PersonRole.Other: return FilterField.None;
  }
};
