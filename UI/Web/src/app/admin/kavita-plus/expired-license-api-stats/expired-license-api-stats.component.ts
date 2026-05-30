import {ChangeDetectionStrategy, Component, inject, signal} from '@angular/core';
import {LicenseService} from "../../../_services/license.service";
import {KavitaPlusLicenseUsage} from "../../../_models/kavitaplus/kavita-plus-license-usage";
import {TranslocoDirective} from "@jsverse/transloco";
import {KavitaPlusApiNameRenderDataPipe} from "../../../_pipes/kavita-plus-api-name-render-data.pipe";
import {CompactNumberPipe} from "../../../_pipes/compact-number.pipe";

@Component({
  selector: 'app-expired-license-api-stats',
  imports: [
    TranslocoDirective,
    KavitaPlusApiNameRenderDataPipe,
    CompactNumberPipe
  ],
  templateUrl: './expired-license-api-stats.component.html',
  styleUrl: './expired-license-api-stats.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ExpiredLicenseApiStatsComponent {
  private readonly licenseService = inject(LicenseService);

  usageData = signal<KavitaPlusLicenseUsage | null>(null);

  constructor() {
    this.licenseService.getLicenseUsage().subscribe(res => {
      // We only want to show when there is data
      res.stats = [...res.stats.filter(s => s.lifetimeCount > 0)];
      this.usageData.set(res);
    });
  }
}
