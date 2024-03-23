import {ChangeDetectionStrategy, ChangeDetectorRef, Component, inject} from '@angular/core';
import {StatisticsService} from "../../../_services/statistics.service";
import {KavitaPlusMetadataBreakdown} from "../../_models/kavitaplus-metadata-breakdown";
import {TranslocoDirective} from "@ngneat/transloco";
import {PercentPipe} from "@angular/common";

@Component({
  selector: 'app-kavitaplus-metadata-breakdown-stats',
  standalone: true,
  imports: [
    TranslocoDirective,
    PercentPipe
  ],
  templateUrl: './kavitaplus-metadata-breakdown-stats.component.html',
  styleUrl: './kavitaplus-metadata-breakdown-stats.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class KavitaplusMetadataBreakdownStatsComponent {
  private readonly cdRef = inject(ChangeDetectorRef);
  private readonly statsService = inject(StatisticsService);

  breakdown: KavitaPlusMetadataBreakdown | undefined;
  completedStart!: number;
  completedEnd!: number;
  errorStart!: number;
  errorEnd!: number;
  percentDone!: number;

  constructor() {
    this.statsService.getKavitaPlusMetadataBreakdown().subscribe(res => {
      this.breakdown = res;
      this.completedStart = 0;
      this.completedEnd = ((res.seriesCompleted - res.erroredSeries) / res.totalSeries);
      this.errorStart = this.completedEnd;
      this.errorEnd = Math.max(1, ((res.seriesCompleted) / res.totalSeries));
      this.percentDone = res.seriesCompleted / res.totalSeries;
      this.cdRef.markForCheck();
    });
  }
}
