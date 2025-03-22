import {ChangeDetectionStrategy, ChangeDetectorRef, Component, inject, Input, OnInit} from '@angular/core';
import {TranslocoDirective, TranslocoService} from "@jsverse/transloco";
import {NgbActiveModal} from "@ng-bootstrap/ng-bootstrap";
import {UpdateVersionEvent} from "../../../_models/events/update-version-event";
import {UpdateSectionComponent} from "../update-section/update-section.component";
import {SafeHtmlPipe} from "../../../_pipes/safe-html.pipe";
import {VersionService} from "../../../_services/version.service";
import {ChangelogUpdateItemComponent} from "../changelog-update-item/changelog-update-item.component";

/**
 * This modal is used when an update occurred and the UI needs to be refreshed to get the latest JS libraries
 */
@Component({
  selector: 'app-new-update-modal',
  standalone: true,
  imports: [
    TranslocoDirective,
    ChangelogUpdateItemComponent
  ],
  templateUrl: './new-update-modal.component.html',
  styleUrl: './new-update-modal.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class NewUpdateModalComponent {

  private readonly ngbModal = inject(NgbActiveModal);
  private readonly translocoService = inject(TranslocoService);

  @Input({required: true}) version: string = '';
  @Input({required: true}) update: UpdateVersionEvent | null = null;

  close() {
    this.ngbModal.dismiss();
  }

  refresh() {
    this.bustLocaleCache();
    this.applyUpdate(this.version);
    // Refresh manually
    location.reload();
  }

  private applyUpdate(version: string): void {
    this.bustLocaleCache();
    location.reload();
  }

  private bustLocaleCache() {
    localStorage.removeItem('@transloco/translations/timestamp');
    localStorage.removeItem('@transloco/translations');
    localStorage.removeItem('translocoLang');
    const locale = localStorage.getItem('kavita-locale') || 'en';
    (this.translocoService as any).cache.delete(locale);
    (this.translocoService as any).cache.clear();

    // Retrigger transloco
    setTimeout(() => {
      this.translocoService.setActiveLang(locale);
    }, 10);
  }

}
