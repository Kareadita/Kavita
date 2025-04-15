export enum MetadataSettingField {
  Summary = 1,
  PublicationStatus = 2,
  StartDate = 3,
  Genres = 4,
  Tags = 5,
  LocalizedName = 6,
  Covers = 7,
  AgeRating = 8,
  People = 9,

  // Chapter fields
  ChapterTitle = 10,
  ChapterSummary = 11,
  ChapterReleaseDate = 12,
  ChapterPublisher = 13,
  ChapterCovers = 14,
}

export const allMetadataSettingField = Object.keys(MetadataSettingField)
  .filter(key => !isNaN(Number(key)) && parseInt(key, 10) >= 0)
  .map(key => parseInt(key, 10)) as MetadataSettingField[];

