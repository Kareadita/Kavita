import {ChangeDetectionStrategy, Component, computed, input, Resource} from '@angular/core';
import {Breakdown} from "../../_models/breakdown";
import {StatCount} from "../../_models/stat-count";
import {LoadingComponent} from "../../../shared/loading/loading.component";
import {TranslocoDirective} from "@jsverse/transloco";
import {BarChartComponent} from "../../../shared/_charts/bar-chart/bar-chart.component";

@Component({
  selector: 'app-string-breakdown',
  imports: [
    BarChartComponent,
    LoadingComponent,
    TranslocoDirective
  ],
  templateUrl: './string-breakdown.component.html',
  styleUrl: './string-breakdown.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class StringBreakdownComponent {

  userId = input.required<number>();
  userName = input.required<string>();
  breakDown = input.required<Resource<Breakdown<string> | undefined>>();
  translationKey = input.required<string>();

  mostUsed = computed(() => {
    if (!this.breakDown().hasValue()) return null;

    const breakdown = this.breakDown().value()!;
    if (breakdown.data.length === 0) return null;

    return breakdown.data
      .reduce((prev, cur) => prev.count > cur.count ? prev : cur,
        {count: -1, value: ""});
  });

  labels = computed(() => this.breakDown().value()?.data.map(d => d.value).reverse() ?? []);
  labelsRight = computed(() => this.breakDown().value()?.data
    .map(d => this.labelFormatter(d)).reverse() ?? []);

  data = computed(() => this.breakDown().value()?.data.map(d => d.count).reverse() ?? []);
  totalReads = computed(() => this.breakDown().value()?.total ?? 0);

  labelFormatter = (params: StatCount<string>) => {
    const percent = ((params.count / this.totalReads()) * 100).toFixed(1);
    return `${params.count} (${percent}%)`;
  }

}
