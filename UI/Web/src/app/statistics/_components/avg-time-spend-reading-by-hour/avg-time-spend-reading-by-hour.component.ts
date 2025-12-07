import {ChangeDetectionStrategy, Component, computed, input, Resource} from '@angular/core';
import {StatCount} from "../../_models/stat-count";
import {TranslocoDirective} from "@jsverse/transloco";
import {UtcToLocalTimePipe} from "../../../_pipes/utc-to-local-time.pipe";
import {LoadingComponent} from "../../../shared/loading/loading.component";
import {BarChartComponent} from "../../../shared/_charts/bar-chart/bar-chart.component";

export type ReadTimeByHour = {
  dataSince: string,
  stats: StatCount<number>[]
}
@Component({
  selector: 'app-avg-time-spend-reading-by-hour',
  imports: [
    TranslocoDirective,
    BarChartComponent,
    UtcToLocalTimePipe,
    LoadingComponent
  ],
  templateUrl: './avg-time-spend-reading-by-hour.component.html',
  styleUrl: './avg-time-spend-reading-by-hour.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class AvgTimeSpendReadingByHourComponent {

  userName = input.required<string>();
  timeByHourResource = input.required<Resource<ReadTimeByHour | undefined>>();

  mostTimeSpentReading = computed(() => {
    const rsc = this.timeByHourResource();
    if (!rsc.hasValue()) return null;

    const statCount = rsc.value()!.stats.reduce((prev, cur)=>
      prev.count >= cur.count ? prev : cur, {count: 0, value: -1});

    if (statCount.value === -1) return null;

    return statCount;
  });

  startHourLocalized = computed(() => {
    const data = this.mostTimeSpentReading();
    if (!data) return null;

    return this.localizeHour(data.value);
  });

  endHourLocalized = computed(() => {
    const data = this.mostTimeSpentReading();
    if (!data) return null;

    return this.localizeHour((data.value + 1) % 24);
  });

  axisLabels = computed(() => {
    return Array.from({length: 24}, (_, i) => this.localizeHour(i));
  });

  labelFormatter = (input: any) => {
    const amount = input.data as number;
    return amount > 0 ? amount + 'm' : '';
  }

  data = computed(() => {
    if (!this.timeByHourResource().hasValue()) return [];

    return this.timeByHourResource().value()!.stats.map(sc => sc.count)
  });

  dataSince = computed(() => {
    if (!this.timeByHourResource().hasValue()) return null;

    return this.timeByHourResource().value()!.dataSince;
  });

  private localizeHour(slot: number) {
      const locale = navigator.language;
      const use12Hours = Intl.DateTimeFormat(locale,  { hour: 'numeric' }).resolvedOptions().hour12 ?? false;

    if (use12Hours) {
      const hour = slot % 12 || 12;
      const period = slot < 12 ? 'am' : 'pm';
      return `${hour}${period}`;
    }

    return `${slot}h`;
  }

}
