import {ChangeDetectionStrategy, Component, computed, input, Resource} from '@angular/core';
import {StatBucket} from "../../_models/stats/stat-bucket";
import {SpreadStats} from "../../_models/stats/spread-stats";
import {translate, TranslocoDirective} from "@jsverse/transloco";
import {BarChartComponent, ToolTipFormatterContext} from "../bar-chart/bar-chart.component";
import {LoadingComponent} from "../../../shared/loading/loading.component";

@Component({
  selector: 'app-bucket-spread-chart',
  imports: [
    TranslocoDirective,
    BarChartComponent,
    LoadingComponent
  ],
  templateUrl: './bucket-spread-chart.component.html',
  styleUrl: './bucket-spread-chart.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class BucketSpreadChartComponent {

  userId = input.required<number>();
  userName = input.required<string>();
  translationKey = input.required<string>();
  bucketSpreadResource = input.required<Resource<SpreadStats | undefined>>();
  endRangeFallback = input<string>('');

  protected readonly highestBucket = computed(() => {
    if (!this.bucketSpreadResource().hasValue()) return null;

    return this.rangeFormatter(this.bucketSpreadResource().value()!.buckets
      .reduce((prev, cur) => prev.count > cur.count ? prev : cur, {
        count: -1,
        rangeStart: 0,
        rangeEnd: 0,
        percentage: 0
      }));
  });

  protected data = computed(() => this.bucketSpreadResource().value()?.buckets.map(d => d.count) ?? []);
  protected percentages = computed(() => this.bucketSpreadResource().value()?.buckets.map(d => d.percentage) ?? []);
  protected labels = computed(() => this.bucketSpreadResource().value()?.buckets
    .map(d => this.rangeFormatter(d)) ?? []);

  rangeFormatter = (params: StatBucket) => {
    const end = params.rangeEnd ?? this.endRangeFallback();
    if (!end) return `${params.rangeStart}+`

    return `${params.rangeStart}-${params.rangeEnd ?? this.endRangeFallback()}`;
  }

  toolTipFormatter = (ctx: ToolTipFormatterContext) => {
    const event = (Array.isArray(ctx.event) ? ctx.event[0] : ctx.event);

    const data = event.data;
    const range = event.name;

    const index = event.dataIndex;
    const percentage = Math.floor(this.percentages()[index]*10)/10;

    return `
    <div class="d-flex flex-column">
        <span>${translate(this.translationKey() + '.data-type', {data: data})}</span>
        <span>${translate(this.translationKey() + '.data-of', {percentage: percentage})}</span>
        <span>${translate(this.translationKey() + '.range', {range: range})}</span>
    </div>
    `;
  }

}
