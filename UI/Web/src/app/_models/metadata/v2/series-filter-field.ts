import {PersonRole} from "../person";

export enum SeriesFilterField
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
    ReadLast = 32,
    FileSize = 33,
}


const enumArray = Object.keys(SeriesFilterField)
    .filter(key => !isNaN(Number(key)) && parseInt(key, 10) >= 0)
    .map(key => {
        // @ts-ignore
        return ({key: key, value: SeriesFilterField[key]});
    });

enumArray.sort((a, b) => a.value.localeCompare(b.value));

export const allSeriesFilterFields = enumArray
    .map(key => parseInt(key.key, 10))as SeriesFilterField[];

export const allSeriesPeopleFields = [
  SeriesFilterField.Characters,
  SeriesFilterField.Colorist,
  SeriesFilterField.CoverArtist,
  SeriesFilterField.Editor,
  SeriesFilterField.Inker,
  SeriesFilterField.Letterer,
  SeriesFilterField.Penciller,
  SeriesFilterField.Publisher,
  SeriesFilterField.Translators,
  SeriesFilterField.Writers,
];

export const personRoleForSeriesFilterField = (role: PersonRole) => {
  switch (role) {
    case PersonRole.Character: return SeriesFilterField.Characters;
    case PersonRole.Colorist: return SeriesFilterField.Colorist;
    case PersonRole.CoverArtist: return SeriesFilterField.CoverArtist;
    case PersonRole.Editor: return SeriesFilterField.Editor;
    case PersonRole.Inker: return SeriesFilterField.Inker;
    case PersonRole.Letterer: return SeriesFilterField.Letterer;
    case PersonRole.Penciller: return SeriesFilterField.Penciller;
    case PersonRole.Publisher: return SeriesFilterField.Publisher;
    case PersonRole.Translator: return SeriesFilterField.Translators;
    case PersonRole.Writer: return SeriesFilterField.Writers;
    case PersonRole.Imprint: return SeriesFilterField.Imprint;
    case PersonRole.Location: return SeriesFilterField.Location;
    case PersonRole.Team: return SeriesFilterField.Team;
  }
};
