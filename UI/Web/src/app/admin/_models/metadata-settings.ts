import {AgeRating} from "../../_models/metadata/age-rating";
import {PersonRole} from "../../_models/metadata/person";
import {MetadataSettingField} from "./metadata-setting-field";

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
  enableSummary: boolean;
  enablePublicationStatus: boolean;
  enableRelationships: boolean;
  enablePeople: boolean;
  enableStartDate: boolean;
  enableCoverImage: boolean;
  enableLocalizedName: boolean;
  enableGenres: boolean;
  enableTags: boolean;
  firstLastPeopleNaming: boolean;
  ageRatingMappings: Map<string, AgeRating>;
  fieldMappings: Array<MetadataFieldMapping>;
  blacklist: Array<string>;
  whitelist: Array<string>;
  personRoles: Array<PersonRole>;
  overrides: Array<MetadataSettingField>;
}
