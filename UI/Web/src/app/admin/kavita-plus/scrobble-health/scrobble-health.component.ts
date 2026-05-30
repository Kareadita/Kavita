import {ChangeDetectionStrategy, Component, computed, inject, signal} from '@angular/core';
import {TranslocoDirective} from '@jsverse/transloco';
import {LicenseService} from '../../../_services/license.service';
import {KavitaPlusProviderHealthSnapshot} from '../../../_models/kavitaplus/kavita-plus-provider-health';
import {
  ScrobbleProviderImageComponent
} from '../../../shared/_components/scrobble-provider-image/scrobble-provider-image.component';
import {ScrobbleProviderNamePipe} from '../../../_pipes/scrobble-provider-name.pipe';
import {KavitaPlusProviderHealthStatusPipe} from '../../../_pipes/kavita-plus-provider-health-status.pipe';
import {ScrobbleProviderMediaTitlePipe} from '../../../_pipes/scrobble-provider-media-title.pipe';

@Component({
  selector: 'app-scrobble-health',
  templateUrl: './scrobble-health.component.html',
  styleUrl: './scrobble-health.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    TranslocoDirective,
    ScrobbleProviderImageComponent,
    ScrobbleProviderNamePipe,
    KavitaPlusProviderHealthStatusPipe,
    ScrobbleProviderMediaTitlePipe,
  ],
})
export class ScrobbleHealthComponent {
  private readonly licenseService = inject(LicenseService);

  private readonly _snapshots = signal<KavitaPlusProviderHealthSnapshot[]>([]);
  protected readonly snapshots = this._snapshots.asReadonly();
  protected readonly lastChecked = signal<Date | null>(null);
  protected readonly loading = signal(false);

  constructor() {
    this.load(false);
  }

  protected load(forceCheck: boolean) {
    this.loading.set(true);
    this.licenseService.getProviderHealthSnapshot(forceCheck).subscribe({
      next: data => {
        this._snapshots.set(data);
        this.lastChecked.set(new Date());
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }

  protected formatLatency(ms: number): string {
    if (ms < 1000) return `${Math.round(ms)}ms`;
    return `${(ms / 1000).toFixed(1)}s`;
  }

  protected minutesAgo = computed(() => {
    const checked = this.lastChecked();
    if (!checked) return null;

    return Math.floor((Date.now() - checked.getTime()) / 60_000);
  });
}
