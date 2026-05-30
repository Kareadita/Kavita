import {ChangeDetectionStrategy, Component, computed, inject, signal} from '@angular/core';
import {TranslocoDirective} from "@jsverse/transloco";
import {LicenseService} from "../../../_services/license.service";
import {ApiUsage, KavitaPlusLicenseUsage} from "../../../_models/kavitaplus/kavita-plus-license-usage";
import {KavitaPlusApiNameRenderDataPipe} from "../../../_pipes/kavita-plus-api-name-render-data.pipe";
import {SparklineComponent} from "../../../shared/_charts/sparkline/sparkline.component";
import {CompactNumberPipe} from "../../../_pipes/compact-number.pipe";

type StatRange = 'last30' | 'lifetime';

@Component({
  selector: 'app-license-api-stats',
  imports: [
    TranslocoDirective,
    KavitaPlusApiNameRenderDataPipe,
    SparklineComponent,
    CompactNumberPipe
  ],
  templateUrl: './license-api-stats.component.html',
  styleUrl: './license-api-stats.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class LicenseApiStatsComponent {

  private readonly licenseService = inject(LicenseService);

  usageData = signal<KavitaPlusLicenseUsage | null>(null);
  selectedRange = signal<StatRange>('lifetime');

  usageInfo = computed(() => this.usageData()?.stats ?? []);

  constructor() {
    this.licenseService.getLicenseUsage().subscribe(res => {
      this.usageData.set(res);
    });
  }

  countFor(api: ApiUsage): number {
    return this.selectedRange() === 'lifetime' ? api.lifetimeCount : api.last30DaysCount;
  }

  bucketsFor(api: ApiUsage): number[] {
    const counts = api.dailyBuckets.map(b => b.count);
    return this.selectedRange() === 'lifetime' ? counts : counts.slice(-30);
  }

}
