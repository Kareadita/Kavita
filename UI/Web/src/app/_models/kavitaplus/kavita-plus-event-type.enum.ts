export enum KavitaPlusEventType {
  // Match
  SeriesMatched = 0,
  SeriesMatchFailed = 1,
  SeriesBlacklisted = 2,
  SeriesMatchFixed = 3,
  SeriesDontMatchSet = 4,

  // Metadata - Series
  MetadataFetched = 10,
  MetadataUpdated = 11,
  CoverUpdated = 13,

  // Metadata - Chapter/Issue
  ChapterMetadataUpdated = 20,
  ChapterCoverUpdated = 21,

  // Metadata - People
  PersonCoverUpdated = 30,
  PersonAliasAdded = 31,

  // Metadata - Collections
  CollectionSynced = 40,
  CollectionItemAdded = 41,

  // Scrobble
  ScrobbleEventCreated = 50,
  ScrobbleEventUpdated = 51,
  ScrobbleEventSent = 52,
  ScrobbleEventFailed = 53,
  ScrobbleRateLimitHit = 54,
  ScrobbleEventSkipped = 55,
  ScrobbleHoldRemoved = 56,
  ScrobbleHoldAdded = 57,

  // Sync (global background jobs)
  SyncStarted = 60,
  SyncCompleted = 61,
  SyncFailed = 62,

  // System
  SystemTokenRefresh = 80,
  SystemProviderInfoSync = 81
}
