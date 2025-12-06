import {ChangeDetectionStrategy, Component, computed, input, Resource} from '@angular/core';
import {StatCount} from "../../_models/stat-count";
import {TranslocoDirective} from "@jsverse/transloco";
import {BarChartComponent} from "../bar-chart/bar-chart.component";
import {UtcToLocalTimePipe} from "../../../_pipes/utc-to-local-time.pipe";

export type ReadTimeByHour = {
  dataSince: string,
  stats: StatCount<number>[]
}
@Component({
  selector: 'app-avg-time-spend-reading-by-hour',
  imports: [
    TranslocoDirective,
    BarChartComponent,
    UtcToLocalTimePipe
  ],
  templateUrl: './avg-time-spend-reading-by-hour.component.html',
  styleUrl: './avg-time-spend-reading-by-hour.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class AvgTimeSpendReadingByHourComponent {

  userName = input.required<string>();
  timeByHourResource = input.required<Resource<ReadTimeByHour | undefined>>();

  mostTimeSpendReading = computed(() => {
    const rsc = this.timeByHourResource();
    if (!rsc.hasValue()) return null;

    return rsc.value()!.stats.reduce((prev, cur)=>
      prev.count > cur.count ? prev : cur, {count: -1, value: 0});
  })

  axisLabels = computed(() => {
    const locale = navigator.language;
    const use12Hours = Intl.DateTimeFormat(locale,  { hour: 'numeric' }).resolvedOptions().hour12 ?? false;

    return Array.from({length: 24}, (_, i) => {
      if (use12Hours) {
        const hour = i % 12 || 12;
        const period = i < 12 ? 'am' : 'pm';
        return `${hour}${period}`;
      }

      return `${i}h`;
    });
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

}
