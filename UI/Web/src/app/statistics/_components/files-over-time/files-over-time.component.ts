import {ChangeDetectionStrategy, Component, computed, inject} from '@angular/core';
import {TranslocoDirective} from "@jsverse/transloco";
import {LineChartComponent} from "../../../shared/_charts/line-chart/line-chart.component";
import {StatisticsService} from "../../../_services/statistics.service";
import {StatCountWithFormat} from "../../_models/stat-count";
import {MangaFormatPipe} from "../../../_pipes/manga-format.pipe";
import {MangaFormat} from "../../../_models/manga-format";
import {DecimalPipe} from "@angular/common";
import {StatsNoDataComponent} from "../../../common/stats-no-data/stats-no-data.component";

// TODO: Make this derived from localeService
const dateOptions: Intl.DateTimeFormatOptions = { month: 'short', day: 'numeric' };

@Component({
  selector: 'app-files-over-time',
  imports: [
    TranslocoDirective,
    LineChartComponent,
    DecimalPipe,
    StatsNoDataComponent
  ],
  templateUrl: './files-over-time.component.html',
  styleUrl: './files-over-time.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class FilesOverTimeComponent {

  private readonly statService = inject(StatisticsService);
  private readonly mangaFormatPipe = new MangaFormatPipe();

  private filesOverTimeResource = this.statService.getFilesAddedOverTime();

  chartData = computed(() => {
    const data = this.filesOverTimeResource.value();
    return this.transformData(data || []);
  });

  isLoading = computed(() => this.filesOverTimeResource.isLoading());

  mostFilesInADay = computed(() => {
    const { data, axisLabels, hasData } = this.chartData();

    if (!hasData || axisLabels.length === 0) {
      return { date: '', count: 0 };
    }

    const totals = axisLabels.map((_, dateIdx) =>
      data.reduce((sum, formatRow) => sum + formatRow[dateIdx], 0)
    );

    const maxCount = Math.max(...totals);
    const maxIndex = totals.indexOf(maxCount);

    return { date: axisLabels[maxIndex], count: maxCount };
  })

  private transformData(data: StatCountWithFormat<any>[]) {
    if (!data || data.length === 0) {
      return { axisLabels: [], legendLabels: [], data: [], hasData: false };
    }

    const uniqueDates = [...new Set(data.map(d => new Date(d.value).getTime()))]
      .sort((a, b) => a - b);

    const axisLabels = uniqueDates.map(ts =>
      new Date(ts).toLocaleDateString('en-US', dateOptions)
    );

    const presentFormats = [...new Set(data.map(d => d.format))].sort();
    const legendLabels = presentFormats.map(f => this.mangaFormatPipe.transform(f));

    const dateIndexMap = new Map<number, number>();
    uniqueDates.forEach((ts, idx) => dateIndexMap.set(ts, idx));

    const formatIndexMap = new Map<MangaFormat, number>();
    presentFormats.forEach((format, idx) => formatIndexMap.set(format, idx));

    const chartData: number[][] = presentFormats.map(() =>
      new Array(uniqueDates.length).fill(0)
    );

    for (const entry of data) {
      const dateTs = new Date(entry.value).getTime();
      const dateIdx = dateIndexMap.get(dateTs);
      const formatIdx = formatIndexMap.get(entry.format);

      if (dateIdx !== undefined && formatIdx !== undefined) {
        chartData[formatIdx][dateIdx] = entry.count;
      }
    }

    return {
      axisLabels,
      legendLabels,
      data: chartData,
      hasData: true
    };
  }
}
