import {ChangeDetectionStrategy, Component, computed, inject} from '@angular/core';
import {translate, TranslocoDirective} from "@jsverse/transloco";
import {LineChartComponent} from "../../../shared/_charts/line-chart/line-chart.component";
import {StatisticsService} from "../../../_services/statistics.service";
import {StatCount} from "../../_models/stat-count";

// TODO: Make this derived from localeService
const dateOptions: Intl.DateTimeFormatOptions = { month: 'short', day: 'numeric' };

@Component({
  selector: 'app-files-over-time',
  imports: [
    TranslocoDirective,
    LineChartComponent
  ],
  templateUrl: './files-over-time.component.html',
  styleUrl: './files-over-time.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class FilesOverTimeComponent {

  private readonly statService = inject(StatisticsService);

  private filesOverTimeResource = this.statService.getFilesAddedOverTime();

  chartData = computed(() => {
    const data = this.filesOverTimeResource.value();
    return this.transformData(data || []);
  });

  isLoading = computed(() => this.filesOverTimeResource.isLoading());

  private transformData(data: StatCount<string | Date>[]) {
    if (!data || data.length === 0) {
      return { axisLabels: [], legendLabels: [], data: [], hasData: false };
    }

    // Sort by date
    const sortedData = [...data].sort((a, b) =>
      new Date(a.value).getTime() - new Date(b.value).getTime()
    );

    const axisLabels = sortedData.map(d =>
      new Date(d.value).toLocaleDateString('en-US', dateOptions)
    );

    // Single series: "Files Added"
    const legendLabels = [translate('files-over-time.legend-label')]; // Or use t('legend-label') for i18n
    const chartData = [sortedData.map(d => d.count)];

    return {
      axisLabels,
      legendLabels,
      data: chartData,
      hasData: true
    };
  }
}
