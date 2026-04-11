import {ReadingList, ReadingListItem} from "../reading-list/reading-list";
import {Chapter} from "../chapter";
import {Series} from "../series";
import {RelationKind} from "../series-detail/relation-kind";
import {Volume} from "../volume";
import {UserCollection} from "../collection-tag";
import {LibraryType} from "../library/library";
import {PageBookmark} from "../readers/page-bookmark";
import {RelatedSeriesPair} from "../../_single-module/related-tab/related-tab.component";
import {SeriesGroup} from "../series-group";

/**
 * Discriminated union representing any entity that can be displayed as a card.
 * Backend returns entityType + data; UI patches in context properties as needed.
 *
 * Usage:
 *   if (entity.entityType === 'series') {
 *     // TypeScript knows entity.data is Series
 *     console.log(entity.data.libraryId);
 *   }
 */
export type CardEntity =
  | SeriesCardEntity
  | CollectionCardEntity
  | ReadingListCardEntity
  | VolumeCardEntity
  | ChapterCardEntity
  | ReadingListItemCardEntity
  | BookmarkCardEntity
  | RelatedSeriesCardEntity
  | RecentlyUpdatedSeriesCardEntity;

export interface SeriesCardEntity {
  entityType: 'series';
  data: Series;
  /** UI-patched: Relationship to another series when displayed in relations context */
  relation?: RelationKind;
  /** UI-patched: Whether this card appears in the On Deck stream */
  isOnDeck?: boolean;
}

export interface CollectionCardEntity {
  entityType: 'collection';
  data: UserCollection;
}

export interface ReadingListCardEntity {
  entityType: 'readinglist';
  data: ReadingList;
}

export interface VolumeCardEntity {
  entityType: 'volume';
  data: Volume;
  /** Required context for routing and actions */
  seriesId: number;
  libraryId: number;
}

export interface ChapterCardEntity {
  entityType: 'chapter';
  data: Chapter;
  /** Required context for routing and actions */
  seriesId: number;
  libraryId: number;
  /** UI-patched: Library type affects title rendering */
  libraryType?: LibraryType;
  /** UI-patched: Suppresses "cannot read" warning for special cases */
  suppressArchiveWarning?: boolean;
}

export interface ReadingListItemCardEntity {
  entityType: 'readinglist-item';
  data: ReadingListItem;
}

export interface BookmarkCardEntity {
  entityType: 'bookmark';
  data: PageBookmark;
}

export interface RelatedSeriesCardEntity {
  entityType: 'related';
  data: RelatedSeriesPair;
}
export interface RecentlyUpdatedSeriesCardEntity {
  entityType: 'recentlyUpdatedSeries';
  data: SeriesGroup;
}

/**
 * Type guard utilities for working with CardEntity
 */
export const CardEntityGuards = {
  isSeries: (e: CardEntity): e is SeriesCardEntity => e.entityType === 'series',
  isCollection: (e: CardEntity): e is CollectionCardEntity => e.entityType === 'collection',
  isReadingList: (e: CardEntity): e is ReadingListCardEntity => e.entityType === 'readinglist',
  isVolume: (e: CardEntity): e is VolumeCardEntity => e.entityType === 'volume',
  isChapter: (e: CardEntity): e is ChapterCardEntity => e.entityType === 'chapter',
  isReadingListItem: (e: CardEntity): e is ReadingListItemCardEntity => e.entityType === 'readinglist-item',
  isBookmark: (e: CardEntity): e is BookmarkCardEntity => e.entityType === 'bookmark',
  isRelatedSeries: (e: CardEntity): e is RelatedSeriesCardEntity => e.entityType === 'related',
  isRecentlyUpdatedSeries: (e: CardEntity): e is RecentlyUpdatedSeriesCardEntity => e.entityType === 'recentlyUpdatedSeries',
};

/**
 * Helper to extract the underlying data with proper typing
 */
export function getCardEntityData<T extends CardEntity>(entity: T): T['data'] {
  return entity.data;
}

/**
 * Helper to create CardEntity wrappers (useful for UI patching)
 */
export const CardEntityFactory = {
  series: (data: Series, context?: Partial<Omit<SeriesCardEntity, 'entityType' | 'data'>>): SeriesCardEntity => ({
    entityType: 'series',
    data,
    ...context
  }),

  collection: (data: UserCollection): CollectionCardEntity => ({
    entityType: 'collection',
    data
  }),

  readingList: (data: ReadingList): ReadingListCardEntity => ({
    entityType: 'readinglist',
    data
  }),

  volume: (data: Volume, seriesId: number, libraryId: number): VolumeCardEntity => ({
    entityType: 'volume',
    data,
    seriesId,
    libraryId
  }),

  chapter: (data: Chapter, seriesId: number, libraryId: number, context?: Partial<Omit<ChapterCardEntity, 'entityType' | 'data' | 'seriesId' | 'libraryId'>>): ChapterCardEntity => ({
    entityType: 'chapter',
    data,
    seriesId,
    libraryId,
    ...context
  }),

  readingListItem: (data: ReadingListItem): ReadingListItemCardEntity => ({
    entityType: 'readinglist-item',
    data
  }),

  bookmark: (data: PageBookmark): BookmarkCardEntity => ({
    entityType: 'bookmark',
    data
  }),

  related: (data: RelatedSeriesPair): RelatedSeriesCardEntity => ({
    entityType: 'related',
    data
  }),

  recentlyUpdatedSeries: (data: SeriesGroup): RecentlyUpdatedSeriesCardEntity => ({
    entityType: 'recentlyUpdatedSeries',
    data
  }),
};
