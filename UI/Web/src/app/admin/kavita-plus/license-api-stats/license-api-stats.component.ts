import {ChangeDetectionStrategy, Component, computed, inject, signal} from '@angular/core';
import {TranslocoDirective} from "@jsverse/transloco";
import {LicenseService} from "../../../_services/license.service";
import {KavitaPlusLicenseUsage} from "../../../_models/kavitaplus/kavita-plus-license-usage";
import {KavitaPlusApiNameRenderDataPipe} from "../../../_pipes/kavita-plus-api-name-render-data.pipe";

@Component({
  selector: 'app-license-api-stats',
  imports: [
    TranslocoDirective,
    KavitaPlusApiNameRenderDataPipe
  ],
  templateUrl: './license-api-stats.component.html',
  styleUrl: './license-api-stats.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class LicenseApiStatsComponent {

  private readonly licenseService = inject(LicenseService);

  usageData = signal<KavitaPlusLicenseUsage | null>(null);
  filteredUsageInfo = computed(() => {
    const data = this.usageData()?.stats ?? [];

    // TODO: Hook in the filter for the stats

    return data;
  });

  constructor() {
    this.licenseService.getLicenseUsage().subscribe(res => {
      this.usageData.set(res);
    });
  }


}
