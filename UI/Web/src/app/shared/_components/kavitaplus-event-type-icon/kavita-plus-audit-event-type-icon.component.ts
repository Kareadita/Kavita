import {ChangeDetectionStrategy, Component, computed, input} from '@angular/core';
import {KavitaPlusEventType} from '../../../_models/kavitaplus/kavita-plus-event-type.enum';
import {KavitaPlusAuditCategory} from "../../../_models/kavitaplus/kavita-plus-audit-category.enum";

function resolveIcon(type: KavitaPlusEventType): string {
  switch (type) {
    case KavitaPlusEventType.SeriesMatched:          return 'fas fa-table-list';
    case KavitaPlusEventType.SeriesMatchFailed:      return 'fas fa-circle-exclamation';
    case KavitaPlusEventType.SeriesBlacklisted:      return 'fas fa-circle-xmark';
    case KavitaPlusEventType.SeriesMatchFixed:       return 'fas fa-eraser';
    case KavitaPlusEventType.SeriesDontMatchSet:     return 'fas fa-table-cells-row-lock';
    case KavitaPlusEventType.MetadataFetched:        return 'fas fa-magnifying-glass';
    case KavitaPlusEventType.MetadataUpdated:        return 'fas fa-database';
    case KavitaPlusEventType.CoverUpdated:           return 'fas fa-image';
    case KavitaPlusEventType.ChapterMetadataUpdated: return 'fas fa-database';
    case KavitaPlusEventType.ChapterCoverUpdated:    return 'fas fa-image';
    case KavitaPlusEventType.PersonCoverUpdated:     return 'fas fa-database';
    case KavitaPlusEventType.PersonAliasAdded:       return 'fas fa-person-circle-plus';
    case KavitaPlusEventType.CollectionSynced:       return 'fas fa-folder-open';
    case KavitaPlusEventType.CollectionItemAdded:    return 'fas fa-folder-plus';
    case KavitaPlusEventType.ScrobbleEventCreated:   return 'fa-regular fa-bookmark';
    case KavitaPlusEventType.ScrobbleEventUpdated:   return 'fa-solid fa-bookmark';
    case KavitaPlusEventType.ScrobbleEventSent:      return 'fas fa-paper-plane';
    case KavitaPlusEventType.ScrobbleEventFailed:    return 'fas fa-circle-exclamation';
    case KavitaPlusEventType.ScrobbleRateLimitHit:   return 'fas fa-circle-xmark';
    case KavitaPlusEventType.ScrobbleEventSkipped:   return 'fas fa-circle-xmark';
    case KavitaPlusEventType.ScrobbleHoldRemoved:    return 'fas fa-eraser';
    case KavitaPlusEventType.ScrobbleHoldAdded:      return 'fas fa-table-cells-row-lock';
    case KavitaPlusEventType.SyncStarted:            return 'fas fa-cloud-arrow-up';
    case KavitaPlusEventType.SyncCompleted:          return 'fas fa-cloud-arrow-down';
    case KavitaPlusEventType.SyncFailed:             return 'fas fa-cloud-arrow-down';
    case KavitaPlusEventType.SystemTokenRefresh:     return 'fas fas-recycle'
    case KavitaPlusEventType.SystemProviderInfoSync: return 'fas fa-sync'
    default:                                         return 'fas fa-circle-exclamation';
  }
}

function resolveColor(type: KavitaPlusEventType): string {
  switch (type) {
    case KavitaPlusEventType.SeriesMatchFailed:
    case KavitaPlusEventType.ScrobbleEventFailed:
    case KavitaPlusEventType.SyncFailed:
      return 'var(--error-color)';
    case KavitaPlusEventType.SeriesBlacklisted:
    case KavitaPlusEventType.ScrobbleRateLimitHit:
    case KavitaPlusEventType.ScrobbleEventSkipped:
      return 'var(--warning-color)';
    default:
      return '';
  }
}

function resolveCategory(type: KavitaPlusAuditCategory): string {
  switch (type) {
    case KavitaPlusAuditCategory.Match:
      return 'var(--audit-log-match-color)';
    case KavitaPlusAuditCategory.Metadata:
      return 'var(--audit-log-metadata-color)';
    case KavitaPlusAuditCategory.Scrobble:
      return 'var(--audit-log-scrobble-color)';
    case KavitaPlusAuditCategory.Sync:
      return 'var(--audit-log-sync-color)';
    case KavitaPlusAuditCategory.System:
      return 'var(--audit-log-system-color)';
  }
}

@Component({
  selector: 'app-kavitaplus-audit-event-type-icon',
  templateUrl: './kavita-plus-audit-event-type-icon.component.html',
  styleUrl: './kavita-plus-audit-event-type-icon.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class KavitaPlusAuditEventTypeIconComponent {
  type = input.required<KavitaPlusEventType>();
  /** Category will override colors when there is not an explicit color designation (error/warning) */
  category = input.required<KavitaPlusAuditCategory>();

  protected readonly iconClass = computed(() => resolveIcon(this.type()));
  protected readonly iconColor = computed(() => {

    const color = resolveColor(this.type());
    const categoryColor = resolveCategory(this.category());

    if (color === '') return categoryColor;

    return color;
  });
}
