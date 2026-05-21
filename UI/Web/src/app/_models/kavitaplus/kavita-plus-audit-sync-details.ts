export interface KavitaPlusAuditSyncDetails {
  // CollectionSynced
  collectionName: string | null;
  stackId: string | null;
  itemCount: number | null;
  missingCount: number | null;

  // CollectionItemAdded
  seriesName: string | null;
  seriesId: number | null;

  // SyncCompleted (WantToRead)
  userName: string | null;
  hasMal: boolean | null;
  hasAniList: boolean | null;
  seriesMatched: number | null;
}
