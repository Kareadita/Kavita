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
  Name = 10,

  // Chapter fields
  ChapterTitle = 20,
  ChapterSummary = 21,
  ChapterReleaseDate = 22,
  ChapterPublisher = 23,
  ChapterCovers = 24,
  ChapterAgeRating = 25,

  // Volume fields
  VolumeCovers = 40
}

export const allMetadataSettingField = Object.keys(MetadataSettingField)
  .filter(key => !isNaN(Number(key)) && parseInt(key, 10) >= 0)
  .map(key => parseInt(key, 10)) as MetadataSettingField[];

