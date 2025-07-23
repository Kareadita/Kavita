
export enum ImportMode {
  Replace = 0,
  Merge = 1,
}

export const ImportModes = [ImportMode.Replace, ImportMode.Merge];

export enum ConflictResolution {
  Manual = 0,
  Keep = 1,
  Replace = 2,
}

export const ConflictResolutions = [ConflictResolution.Manual, ConflictResolution.Keep, ConflictResolution.Replace];

export interface ImportSettings {
  importMode: ImportMode;
  resolution: ConflictResolution;
  whitelist: boolean;
  blacklist: boolean;
  ageRatings: boolean;
  fieldMappings: boolean;
  ageRatingConflictResolutions: Record<string, ConflictResolution>;
  fieldMappingsConflictResolutions: Record<number, ConflictResolution>;
}

export interface ImportConflict {
  /** The ID of the entity in the DB */
  oldId: number;
  /** The ID of the entity from the imported JSON */
  newId: number;
}

export interface FieldMappingsImportResult {
  success: boolean;
  ageRatingConflicts: string[];
  fieldMappingConflicts: ImportConflict[];
}
