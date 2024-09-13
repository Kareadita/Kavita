import {ChangeDetectionStrategy, ChangeDetectorRef, Component, inject} from '@angular/core';
import {StatisticsService} from "../../../_services/statistics.service";
import {KavitaPlusMetadataBreakdown} from "../../_models/kavitaplus-metadata-breakdown";
import {TranslocoDirective} from "@jsverse/transloco";
import {PercentPipe} from "@angular/common";
import {NgbProgressbar, NgbProgressbarStacked, NgbTooltip} from "@ng-bootstrap/ng-bootstrap";

@Component({
  selector: 'app-kavitaplus-metadata-breakdown-stats',
  standalone: true,
  imports: [
    TranslocoDirective,
    PercentPipe,
    NgbProgressbarStacked,
    NgbProgressbar,
    NgbTooltip
  ],
  templateUrl: './kavitaplus-metadata-breakdown-stats.component.html',
  styleUrl: './kavitaplus-metadata-breakdown-stats.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class KavitaplusMetadataBreakdownStatsComponent {
  private readonly cdRef = inject(ChangeDetectorRef);
  private readonly statsService = inject(StatisticsService);

  breakdown: KavitaPlusMetadataBreakdown | undefined;

  errorPercent!: number;
  completedPercent!: number;


  constructor() {
    this.statsService.getKavitaPlusMetadataBreakdown().subscribe(res => {
      this.breakdown = res;

      this.errorPercent = (res.erroredSeries / res.totalSeries) * 100;
      this.completedPercent = ((res.seriesCompleted - res.erroredSeries) / res.totalSeries) * 100;

      this.cdRef.markForCheck();
    });
  }
}
