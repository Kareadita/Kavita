import {AgeRating} from "../../_models/metadata/age-rating";

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
  enableLocalizedName: boolean;
  enableGenres: boolean;
  enableTags: boolean;
  ageRatingMappings: Map<string, AgeRating>;
  fieldMappings: Array<MetadataFieldMapping>;
  blacklist: Array<string>;
}
