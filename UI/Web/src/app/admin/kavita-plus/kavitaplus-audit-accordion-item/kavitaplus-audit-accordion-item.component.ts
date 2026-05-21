import {ChangeDetectionStrategy, Component, computed, inject, input, signal} from '@angular/core';
import {NgbCollapse} from '@ng-bootstrap/ng-bootstrap';
import {NgClass} from '@angular/common';
import {Router} from '@angular/router';
import {TranslocoDirective} from '@jsverse/transloco';
import {KavitaPlusAuditEntry} from '../../../_models/kavitaplus/kavita-plus-audit-entry';
import {KavitaPlusAuditCategory} from '../../../_models/kavitaplus/kavita-plus-audit-category.enum';
import {KavitaPlusEventType} from '../../../_models/kavitaplus/kavita-plus-event-type.enum';
import {AuditStatus} from '../../../_models/kavitaplus/audit-status.enum';
import {ImageService} from '../../../_services/image.service';
import {ImageComponent} from '../../../shared/image/image.component';
import {ProfileIconComponent} from '../../../_single-module/profile-icon/profile-icon.component';
import {
  ScrobbleProviderImageComponent
} from '../../../shared/_components/scrobble-provider-image/scrobble-provider-image.component';
import {ScrobbleProviderNamePipe} from '../../../_pipes/scrobble-provider-name.pipe';
import {KavitaPlusEventTypePipe} from '../../../_pipes/kavita-plus-event-type.pipe';
import {KavitaPlusEventDescriptionPipe} from '../../../_pipes/kavita-plus-event-description.pipe';
import {AuditLogErrorPipe} from '../../../_pipes/audit-log-error.pipe';
import {TimeAgoPipe} from '../../../_pipes/time-ago.pipe';
import {UtcToLocalTimePipe} from '../../../_pipes/utc-to-local-time.pipe';
import {AuditStatusTitlePipe} from "../../../_pipes/audit-status-title.pipe";
import {KavitaplusDiffComponent} from "../kavitaplus-diff/kavitaplus-diff.component";
import {AuditSubjectType} from "../../../_models/kavitaplus/audit-subject-type.enum";

@Component({
  selector: 'app-kavitaplus-audit-accordion-item',
  imports: [
    NgbCollapse,
    NgClass,
    TranslocoDirective,
    ImageComponent,
    ProfileIconComponent,
    ScrobbleProviderImageComponent,
    ScrobbleProviderNamePipe,
    KavitaPlusEventTypePipe,
    KavitaPlusEventDescriptionPipe,
    AuditLogErrorPipe,
    TimeAgoPipe,
    UtcToLocalTimePipe,
    AuditStatusTitlePipe,
    KavitaplusDiffComponent,
  ],
  templateUrl: './kavitaplus-audit-accordion-item.component.html',
  styleUrl: './kavitaplus-audit-accordion-item.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class KavitaplusAuditAccordionItemComponent {
  protected readonly imageService = inject(ImageService);
  private readonly router = inject(Router);

  entry = input.required<KavitaPlusAuditEntry>();

  collapsed = signal(true);

  statusBadgeClass = computed(() => {
    switch (this.entry().status) {
      case AuditStatus.Success:
        return 'bg-success';
      case AuditStatus.Failure:
        return 'bg-danger';
      default:
        return 'bg-secondary';
    }
  });

  supportsDiff(entry: KavitaPlusAuditEntry) {
    return [KavitaPlusEventType.MetadataUpdated, KavitaPlusEventType.ChapterMetadataUpdated].includes(entry.eventType);
  }

  navigateToSeries() {
    const e = this.entry();
    if (e.seriesId == null || e.libraryId == null) return;
    this.router.navigate(['library', e.libraryId, 'series', e.seriesId]);
  }

  protected readonly KavitaPlusAuditCategory = KavitaPlusAuditCategory;
  protected readonly AuditSubjectType = AuditSubjectType;
}
