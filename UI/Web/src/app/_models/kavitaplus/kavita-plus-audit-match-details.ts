import {MetadataProvider} from "./metadata-provider.enum";

export interface AuditMatchExternalIds {
  aniListId: number;
  malId: number;
  mangaBakaId: number;
  cbrId: number;
  hardcoverId: number;
}

export interface KavitaPlusAuditMatchDetails {
  matchedName: string | null;
  before: AuditMatchExternalIds | null;
  after: AuditMatchExternalIds | null;
  reason: string | null;
  dontMatch: boolean | null;
  previousProvider: MetadataProvider | null;
  newProvider: MetadataProvider | null;
  /** False when the Series fell back to its Library's default provider */
  isProviderOverride: boolean | null;
}
