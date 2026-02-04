import {inject, Injectable} from '@angular/core';
import {CardConfigFactory} from "./card-config-factory.service";
import {Chapter} from "../_models/chapter";
import {
  ActionableCardConfiguration,
  BaseCardConfiguration,
  CardConfigurationOverrides
} from "../_models/card/card-configuration";
import {CardEntity, ChapterCardEntity, ReadingListItemCardEntity, VolumeCardEntity} from "../_models/card/card-entity";
import {ReadingList} from "../_models/reading-list";
import {ActionItem} from "./action-factory.service";
import {Series} from "../_models/series";
import {Volume} from "../_models/volume";
import {UserCollection} from "../_models/collection-tag";
import {LibraryType} from "../_models/library/library";
import {User} from "../_models/user/user";

/**
 * Context required to resolve configurations for entities that need
 * additional information not available on the entity itself.
 */
export interface CardResolveContext {
  /** Library type - needed for chapter/volume title rendering */
  libraryType?: LibraryType;

  /** For collections: pre-built actionables (since they need special setup) */
  collectionActionables?: ActionItem<UserCollection>[];

  /** For reading lists: the shouldRender callback */
  readingListShouldRender?: (action: ActionItem<ReadingList>, entity: ReadingList, user: User) => boolean;
}

/**
 * Registry service that resolves CardConfiguration from CardEntity.
 * Use this when working with mixed entity streams (e.g., dashboard).
 *
 * Usage:
 *   private registry = inject(CardConfigRegistry);
 *
 *   // In template loop over CardEntity[]
 *   getConfig(entity: CardEntity) {
 *     return this.registry.resolve(entity, this.handleAction.bind(this));
 *   }
 */
@Injectable({
  providedIn: 'root',
})
export class CardConfigRegistry {
  private readonly factory = inject(CardConfigFactory);

  /**
   * Resolves the appropriate CardConfiguration based on entity type.
   *
   * @param entity - The wrapped card entity
   * @param actionCallback - Callback for action menu items
   * @param context - Additional context needed for certain entity types
   * @param overrides - Optional configuration overrides
   * @returns Fully configured CardConfiguration
   */
  resolve(
    entity: CardEntity,
    actionCallback: (action: ActionItem<any>, entity: any) => void,
    context?: CardResolveContext,
    overrides?: CardConfigurationOverrides<any>
  ): BaseCardConfiguration<any> {
    switch (entity.entityType) {
      case 'series':
        return this.factory.forSeries(
          actionCallback as (action: ActionItem<Series>, s: Series) => void,
          overrides as CardConfigurationOverrides<Series>
        );

      case 'chapter':
        return this.resolveChapter(entity, actionCallback, context, overrides);

      case 'volume':
        return this.resolveVolume(entity, actionCallback, context, overrides);

      case 'collection':
        return this.resolveCollection(context, overrides);

      case 'readinglist':
        return this.resolveReadingList(actionCallback, context, overrides);

      case 'readinglist-item':
        return this.resolveReadingListItem(entity, actionCallback, context, overrides);

      default:
        const _exhaustive: never = entity;
        throw new Error(`Unknown entity type: ${(entity as CardEntity).entityType}`);
    }
  }

  /**
   * Batch resolve for arrays - useful for pre-computing configs.
   * All entities should share the same context and callback.
   */
  resolveAll(
    entities: CardEntity[],
    actionCallback: (action: ActionItem<any>, entity: any) => void,
    context?: CardResolveContext
  ): Map<CardEntity, BaseCardConfiguration<any>> {
    const configMap = new Map<CardEntity, BaseCardConfiguration<any>>();
    for (const entity of entities) {
      configMap.set(entity, this.resolve(entity, actionCallback, context));
    }
    return configMap;
  }

  private resolveChapter(
    entity: ChapterCardEntity,
    actionCallback: (action: ActionItem<any>, entity: any) => void,
    context?: CardResolveContext,
    overrides?: CardConfigurationOverrides<any>
  ): ActionableCardConfiguration<Chapter> {
    const libraryType = entity.libraryType ?? context?.libraryType ?? LibraryType.Manga;

    const mergedOverrides = this.mergeChapterContext(entity, overrides as CardConfigurationOverrides<Chapter>);

    return this.factory.forChapter(
      entity.seriesId,
      entity.libraryId,
      libraryType,
      actionCallback as (action: ActionItem<Chapter>, c: Chapter) => void,
      mergedOverrides
    );
  }

  private resolveVolume(
    entity: VolumeCardEntity,
    actionCallback: (action: ActionItem<any>, entity: any) => void,
    context?: CardResolveContext,
    overrides?: CardConfigurationOverrides<any>
  ): ActionableCardConfiguration<Volume> {
    const libraryType = context?.libraryType ?? LibraryType.Manga;

    return this.factory.forVolume(
      entity.seriesId,
      entity.libraryId,
      libraryType,
      actionCallback as (action: ActionItem<Volume>, v: Volume) => void,
      overrides as CardConfigurationOverrides<Volume>
    );
  }

  private resolveCollection(
    context?: CardResolveContext,
    overrides?: CardConfigurationOverrides<any>
  ): ActionableCardConfiguration<UserCollection> {
    if (!context?.collectionActionables) {
      throw new Error('Collection resolution requires collectionActionables in context');
    }

    return this.factory.forCollection(
      context.collectionActionables,
      undefined, // templateRef - caller should use overrides if needed
      overrides as CardConfigurationOverrides<UserCollection>
    );
  }

  private resolveReadingList(
    actionCallback: (action: ActionItem<any>, entity: any) => void,
    context?: CardResolveContext,
    overrides?: CardConfigurationOverrides<any>
  ): ActionableCardConfiguration<ReadingList> {
    const shouldRender = context?.readingListShouldRender ?? (() => true);

    return this.factory.forReadingList(
      actionCallback as (action: ActionItem<ReadingList>, r: ReadingList) => void,
      shouldRender,
      overrides as CardConfigurationOverrides<ReadingList>
    );
  }

  /**
   * Reading list items wrap other entities - delegate to reading list config
   */
  private resolveReadingListItem(
    entity: ReadingListItemCardEntity,
    actionCallback: (action: ActionItem<any>, entity: any) => void,
    context?: CardResolveContext,
    overrides?: CardConfigurationOverrides<any>
  ): ActionableCardConfiguration<ReadingList> {
    return this.resolveReadingList(actionCallback, context, overrides);
  }

  /**
   * Merges chapter-specific context from wrapper into overrides
   */
  private mergeChapterContext(
    entity: ChapterCardEntity,
    overrides?: CardConfigurationOverrides<Chapter>
  ): CardConfigurationOverrides<Chapter> {
    const contextOverrides: CardConfigurationOverrides<Chapter> = {};

    if (entity.suppressArchiveWarning !== undefined) {
      contextOverrides.showErrorFunc = (c: Chapter) =>
        c.pages === 0 && !entity.suppressArchiveWarning;
    }

    return { ...contextOverrides, ...overrides };
  }
}
