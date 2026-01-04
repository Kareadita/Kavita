import {ChangeDetectionStrategy, Component, computed, input, Resource} from '@angular/core';
import {StatBucket} from "../../_models/stats/stat-bucket";
import {SpreadStats} from "../../_models/stats/spread-stats";
import {translate, TranslocoDirective} from "@jsverse/transloco";
import {LoadingComponent} from "../../../shared/loading/loading.component";
import {CompactNumberPipe} from "../../../_pipes/compact-number.pipe";
import {BarChartComponent, ToolTipFormatterContext} from "../../../shared/_charts/bar-chart/bar-chart.component";
import {StatsNoDataComponent} from "../../../common/stats-no-data/stats-no-data.component";

@Component({
  selector: 'app-bucket-spread-chart',
  imports: [
    TranslocoDirective,
    BarChartComponent,
    LoadingComponent,
    StatsNoDataComponent
  ],
  templateUrl: './bucket-spread-chart.component.html',
  styleUrl: './bucket-spread-chart.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class BucketSpreadChartComponent {

  private readonly compactNumberPipe = new CompactNumberPipe();

  userId = input.required<number>();
  userName = input.required<string>();
  translationKey = input.required<string>();
  bucketSpreadResource = input.required<Resource<SpreadStats | undefined>>();
  endRangeFallback = input<string>('');

  protected readonly spreadStats = computed(() => {
    if (!this.bucketSpreadResource().hasValue()) return null;

    const spreadStats = this.bucketSpreadResource().value()!;
    if (spreadStats.totalCount === 0) return null;

    return spreadStats;
  });

  protected readonly highestBucket = computed(() => {
    const spreadStats = this.spreadStats();
    if (!spreadStats) return null;

    return this.rangeFormatter(spreadStats.buckets
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
    const start = this.compactNumberPipe.transform(params.rangeStart);
    const endRange = params.rangeEnd ?? this.endRangeFallback();
    if (!endRange) return `${start}+`

    const end = typeof endRange === 'string' ? endRange : this.compactNumberPipe.transform(endRange);

    return `${start}-${end}`;
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
