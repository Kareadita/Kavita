import {ChangeDetectionStrategy, Component, computed, inject, input} from '@angular/core';
import {UpdateVersionEvent} from "../../../_models/events/update-version-event";
import {TranslocoDirective, TranslocoService} from "@jsverse/transloco";
import {WikiLink} from "../../../_models/wiki";
import {NgbActiveModal} from "@ng-bootstrap/ng-bootstrap";
import {ChangelogUpdateItemComponent} from "../changelog-update-item/changelog-update-item.component";
import {SafeHtmlPipe} from "../../../_pipes/safe-html.pipe";

@Component({
  selector: 'app-version-update-modal',
  imports: [
    ChangelogUpdateItemComponent,
    TranslocoDirective,
    SafeHtmlPipe
  ],
  templateUrl: './version-update-modal.component.html',
  styleUrl: './version-update-modal.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class VersionUpdateModalComponent {
  private readonly translocoService = inject(TranslocoService);
  private readonly modal = inject(NgbActiveModal);

  /** Determines which modal variant to show, drives messaging and buttons */
  mode = input<'refresh' | 'update-available' | 'out-of-date'>('refresh');
  /** Changelog data, null for 'out-of-date' modal*/
  update = input<UpdateVersionEvent | null>(null);
  /** Number of versions out of date, only applicable for 'out-of-date' modal */
  versionsOutOfDate = input<number>(0);

  isDocker = computed(() => this.update()?.isDocker ?? false);
  /** Wiki help link — Docker or native install guide */
  helpUrl = computed(() => {
    return this.isDocker() ? WikiLink.UpdateDocker : WikiLink.UpdateNative;
  });
  private readonly localePrefix: Record<string, string> = {
    'refresh': 'new-version',
    'update-available': 'update-notification',
    'out-of-date': 'out-of-date',
  };
  title = computed(() => `${this.localePrefix[this.mode()]}.title`);

  close() {
    this.modal.dismiss();
  }

  refresh() {
    this.bustLocaleCache();
    // Refresh manually
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
