import {inject, Injectable} from '@angular/core';
import {CardConfigFactory} from "./card-config-factory.service";
import {Chapter} from "../_models/chapter";
import {CardConfiguration, CardConfigurationOverrides} from "../_models/card/card-configuration";
import {CardEntity, ChapterCardEntity, ReadingListItemCardEntity} from "../_models/card/card-entity";
import {ReadingList} from "../_models/reading-list";
import {ActionableEntity} from "./action-factory.service";

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
   * Due to TypeScript's contravariance on function parameters, we can't directly
   * return CardConfiguration<unknown>. Instead, we use a type-safe internal
   * implementation and cast at the boundary.
   *
   * @param entity - The wrapped card entity
   * @param actionCallback - Callback for action menu items
   * @param overrides - Optional configuration overrides
   * @returns Fully configured CardConfiguration
   */
  resolve<T extends ActionableEntity = ActionableEntity>(
    entity: CardEntity,
    actionCallback: (action: any, entity: any) => void,
    overrides?: CardConfigurationOverrides<any>
  ): CardConfiguration<T> {
    let config: CardConfiguration<any>;

    switch (entity.entityType) {
      case 'series':
        config = this.factory.forSeries(
          actionCallback,
          overrides
        );
        break;

      case 'chapter':
        config = this.factory.forChapter(
          entity.seriesId,
          entity.libraryId,
          actionCallback,
          this.mergeChapterContext(entity, overrides)
        );
        break;

      case 'volume':
        config = this.factory.forVolume(
          entity.seriesId,
          entity.libraryId,
          actionCallback,
          overrides
        );
        break;

      case 'collection':
        config = this.factory.forCollection(
          actionCallback,
          overrides
        );
        break;

      case 'readinglist':
        config = this.factory.forReadingList(
          actionCallback,
          overrides
        );
        break;

      case 'readinglist-item':
        config = this.resolveReadingListItem(
          entity,
          actionCallback,
          overrides
        );
        break;

      default:
        const _exhaustive: never = entity;
        throw new Error(`Unknown entity type: ${(entity as CardEntity).entityType}`);
    }

    return config as CardConfiguration<T>;
  }

  /**
   * Batch resolve for arrays - useful for pre-computing configs
   */
  resolveAll(
    entities: CardEntity[],
    actionCallback: (action: any, entity: any) => void
  ): Map<CardEntity, CardConfiguration<any>> {
    const configMap = new Map<CardEntity, CardConfiguration<any>>();
    for (const entity of entities) {
      configMap.set(entity, this.resolve(entity, actionCallback));
    }
    return configMap;
  }

  /**
   * Reading list items need special handling - they wrap other entities
   */
  private resolveReadingListItem(
    entity: ReadingListItemCardEntity,
    actionCallback: (action: any, entity: ReadingList) => void,
    overrides?: CardConfigurationOverrides<ReadingList>
  ): CardConfiguration<ReadingList> {
    // ReadingListItem contains a reference to the actual entity
    return this.factory.forReadingList(
      actionCallback as (action: any, s: ReadingList) => void,
      overrides as CardConfigurationOverrides<ReadingList>
    );
  }

  /**
   * Merges chapter-specific context from wrapper into overrides
   */
  private mergeChapterContext(
    entity: ChapterCardEntity,
    overrides?: CardConfigurationOverrides<Chapter>
  ): CardConfigurationOverrides<Chapter> {
    const contextOverrides: CardConfigurationOverrides<Chapter> = {};

    // Handle suppressArchiveWarning from wrapper
    if (entity.suppressArchiveWarning !== undefined) {
      contextOverrides.showErrorFunc = (c: Chapter) =>
        c.pages === 0 && !entity.suppressArchiveWarning;
    }

    return { ...contextOverrides, ...overrides };
  }
}
