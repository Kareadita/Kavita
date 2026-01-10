import {ChangeDetectionStrategy, Component, computed, input, Resource} from '@angular/core';
import {StatCount} from "../../_models/stat-count";
import {TranslocoDirective} from "@jsverse/transloco";
import {MonthLabelPipe} from "../../../_pipes/month-label.pipe";
import {DecimalPipe} from "@angular/common";
import {LineChartComponent} from "../../../shared/_charts/line-chart/line-chart.component";
import {StatsNoDataComponent} from "../../../common/stats-no-data/stats-no-data.component";

@Component({
  selector: 'app-reads-by-month',
  imports: [
    TranslocoDirective,
    LineChartComponent,
    DecimalPipe,
    StatsNoDataComponent
  ],
  templateUrl: './reads-by-month.component.html',
  styleUrl: './reads-by-month.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class ReadsByMonthComponent {

  userName = input.required<string>();
  readsByMonthResource = input.required<Resource<StatCount<{year: number, month: number}>[] | undefined>>();

  highestRead = computed(() => {
    if (!this.readsByMonthResource().hasValue()) return undefined;

    const mostRead = (this.readsByMonthResource().value() || []).reduce(
      (prev, cur) => cur.count > prev.count ? cur : prev,
      {count: 0, value: {year: 0, month: 0}});

    if (mostRead.value.month === 0) return undefined;

    const monthLabelPipe = new MonthLabelPipe();
    return {
      year: mostRead.value.year,
      month: monthLabelPipe.transform(mostRead.value.month, false),
      count: mostRead.count,
    }
  });

  legendLabels = computed(() => {
    if (!this.readsByMonthResource().hasValue()) return [];

    const years = this.readsByMonthResource().value()!.map(s => s.value.year);
    return Array.from(new Set(years)).sort((a, b) => a - b).map(y => y + '');
  });

  axisLabels = computed(() => {
    const monthLabelPipe = new MonthLabelPipe();
    return Array.from({length: 12}, (_, i) => monthLabelPipe.transform(i+1, false));
  });

  data = computed(() => {
    if (!this.readsByMonthResource().hasValue()) return [];

    const yearCount = this.legendLabels().length;
    const data = Array.from({ length: yearCount }, () => new Array(12).fill(0));

    this.readsByMonthResource().value()!.forEach(s => {
      const yearIndex = this.legendLabels().indexOf(s.value.year + '');
      if (yearIndex !== -1) {
        data[yearIndex][s.value.month-1] = s.count;
      }
    });

    return data;
  });

}
