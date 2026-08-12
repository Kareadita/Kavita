import {AgeRating} from "../../_models/metadata/age-rating";
import {PersonRole} from "../../_models/metadata/person";
import {MetadataSettingField} from "./metadata-setting-field";
import {TagWeight} from "./tag-weight.enum";

export enum MetadataFieldType {
  Genre = 0,
  Tag = 1
}


export interface MetadataFieldMapping {
  id: number;
  sourceType: MetadataFieldType;
  destinationType: MetadataFieldType;
  sourceValue: string;
  destinationValue: string;
  excludeFromSource: boolean;
}

export interface MetadataSettings {
  enabled: boolean;
  enableExtendedMetadataProcessing: boolean;
  enableSummary: boolean;
  enablePublicationStatus: boolean;
  enableAgeRating: boolean;
  enableRelationships: boolean;
  enablePeople: boolean;
  enableStartDate: boolean;
  enableCoverImage: boolean;
  enableLocalizedName: boolean;
  enableName: boolean;

  enableChapterSummary: boolean;
  enableChapterReleaseDate: boolean;
  enableChapterTitle: boolean;
  enableChapterPublisher: boolean;
  enableChapterCoverImage: boolean;

  enableVolumeCoverImage: boolean;

  enableGenres: boolean;
  enableTags: boolean;
  firstLastPeopleNaming: boolean;
  ageRatingMappings: Record<string, AgeRating>;
  fieldMappings: Array<MetadataFieldMapping>;
  blacklist: Array<string>;
  whitelist: Array<string>;
  personRoles: Array<PersonRole>;
  overrides: Array<MetadataSettingField>;
  filterAboveWeight: TagWeight | null;

  globalLanguageTitleSettings: SeriesNameLanguage;
  libraryLanguageTitleOverrides: Record<string, SeriesNameLanguage>;
  /*Like ageRatingMappings but scoped to ContentRating from metadata sites*/
  externalAgeRatingMappings: Record<string, AgeRating>;
}

export interface SeriesNameLanguage {
  name: string;
  localizedName: string;
}
