export enum MetadataSettingField {
  Summary = 1,
  PublicationStatus = 2,
  StartDate = 3,
  Genres = 4,
  Tags = 5,
  LocalizedName = 6,
  Covers = 7,
  AgeRating = 8,
  People = 9
}

export const allMetadataSettingField = Object.keys(MetadataSettingField)
  .filter(key => !isNaN(Number(key)) && parseInt(key, 10) >= 0)
  .map(key => parseInt(key, 10)) as MetadataSettingField[];

