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
    ReadTime = 23
}

export const allFields = Object.keys(FilterField).filter(key => !isNaN(Number(key)) && parseInt(key, 10) >= 0).map(key => parseInt(key, 10)) as FilterField[];
