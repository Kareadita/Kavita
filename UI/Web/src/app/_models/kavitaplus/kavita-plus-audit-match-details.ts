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
}
