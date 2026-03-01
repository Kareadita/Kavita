import {DownloadEvent} from "../../shared/_services/download.service";
import {Observable} from "rxjs";
import {BulkSelectionEntityDataSource} from "../../cards/bulk-selection.service";
import {CardEntity} from "./card-entity";
import {MangaFormat} from "../manga-format";
import {TemplateRef} from "@angular/core";
import {ActionableEntity} from "../../_services/action-factory.service";
import {IHasProgress} from "../common/i-has-progress";
import {ActionItem} from "../actionables/action-item";
import {UserProgressUpdateEvent} from "../events/user-progress-update-event";

/**
 * Configuration object that defines how a card renders and behaves.
 * Created by CardConfigFactory for each entity type with sensible defaults.
 *
 * @typeParam T - The underlying data type.
 */
export interface BaseCardConfiguration<T> {

  /** Whether bulk selection is enabled for this card */
  allowSelection: boolean;

  /** Entity type identifier for bulk selection tracking */
  selectionType: BulkSelectionEntityDataSource;

  /** Suppress Archive Warning when page count is 0 **/
  suppressArchiveWarning: boolean;

  /** Returns the cover image URL */
  coverFunc: (entity: T) => string;

  /** Returns the primary title displayed in the card footer */
  titleFunc: (entity: T) => string;

  /** Returns the router link for the title */
  titleRouteFunc: (entity: T) => string;

  /** Returns the meta title text (area above the main title). Required as fallback. */
  metaTitleFunc: (entity: T, wrapper: CardEntity) => string;

  /** Returns tooltip text for the title */
  tooltipFunc: (entity: T) => string;

  /** Returns reading progress. Return { pages: 0, pagesRead: 0 } if not applicable. */
  progressFunc: (entity: T) => IHasProgress;

  /** Returns the MangaFormat for the format badge, or null to hide */
  formatBadgeFunc?: (entity: T) => MangaFormat | null;

  /** Returns count for the badge (e.g., volume count, file count). 0 or 1 hides the badge. */
  countFunc?: (entity: T) => number;

  /** Returns true to show the error banner ("cannot read"). Default: pages === 0 */
  showErrorFunc?: (entity: T) => boolean;

  /** Returns accessible label for the card */
  ariaLabelFunc?: (entity: T) => string;

  /**
   * Optional template for title area. Takes precedence over titleFunc.
   * Context: { $implicit: CardEntity } - the full wrapper, not just data
   */
  titleTemplate?: TemplateRef<{ $implicit: CardEntity }>;

  /**
   * Optional template for meta title area. Takes precedence over metaTitleFunc.
   * Context: { $implicit: CardEntity } - the full wrapper, not just data
   */
  metaTitleTemplate?: TemplateRef<{ $implicit: CardEntity }>;

  /** Callback when the read button is clicked */
  readFunc: ((entity: T) => void) | null;

  /** Callback when the card body is clicked (navigation or preview) */
  clickFunc?: (entity: T, wrapper: CardEntity) => void;

  /**
   * Returns an observable of download events for this entity.
   * Used to show download progress indicator.
   */
  downloadObservableFunc?: (entity: T) => Observable<DownloadEvent | null>;
  /**
   * Returns key/values for route params (bookmark mode)
   */
  titleRouteParamsFunc?: (entity: T) => Record<string, any>;

  /**
   * Optional strategy for handling real-time progress updates.
   * If not provided, the card ignores progress events.
   */
  progressUpdateStrategy?: ProgressUpdateStrategy<T>;
}

/**
 * Configuration object that defines how a card renders and behaves.
 * Created by CardConfigFactory for each entity type with sensible defaults.
 *
 * @typeParam T - The underlying data type, T must be ActionableEntity
 */
export interface ActionableCardConfiguration<T extends ActionableEntity>
  extends BaseCardConfiguration<T> {
  /** Action items for the card's action menu */
  actionableFunc: (entity: T) => ActionItem<T>[];
}

export type CardConfiguration<T> = T extends ActionableEntity
  ? ActionableCardConfiguration<T> | BaseCardConfiguration<T>
  : BaseCardConfiguration<T>;

export function hasActionables<T>(
  config: BaseCardConfiguration<T>
): config is BaseCardConfiguration<T> & { actionableFunc: (entity: any) => ActionItem<any>[] } {
  return (
    'actionableFunc' in config &&
    typeof (config as any).actionableFunc === 'function'
  );
}

/**
 * Partial configuration for overrides. All properties optional.
 */
export type CardConfigurationOverrides<T extends ActionableEntity> = Partial<ActionableCardConfiguration<T>>;
export type BaseCardConfigurationOverrides<T> = Partial<BaseCardConfiguration<T>>;


/**
 * Defines how a card entity matches and responds to real-time progress updates.
 * The card component uses this to:
 * 1. Filter relevant SignalR events
 * 2. Apply local updates to the entity
 * 3. Notify the parent via a callback for state synchronization
 */
export interface ProgressUpdateStrategy<T> {
  /**
   * Extract matching criteria from the entity.
   * Used to filter incoming UserProgressUpdateEvent.
   */
  getMatchCriteria: (entity: T) => ProgressMatchCriteria;

  /**
   * Apply the update to the entity and return the new state.
   * Return null if the entity cannot be updated locally (e.g., series without chapter data)
   * and requires a full refetch.
   */
  applyUpdate: (entity: T, event: UserProgressUpdateEvent) => T | null;
}

export interface ProgressMatchCriteria {
  chapterId?: number;
  volumeId?: number;
  seriesId?: number;
}

/**
 * Result emitted after a progress update is processed.
 * Parent components use this to update their state.
 */
export interface ProgressUpdateResult<T> {
  /** The updated entity (null if refetch required) */
  entity: T | null;
  /** The original event that triggered the update */
  event: UserProgressUpdateEvent;
  /** Whether the parent should refetch instead of using the entity */
  requiresRefetch: boolean;
}
