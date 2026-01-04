import {ChangeDetectionStrategy, Component, computed, DestroyRef, inject, input, signal} from '@angular/core';
import {DayOfWeek, StatisticsService} from 'src/app/_services/statistics.service';
import {StatCount} from '../../_models/stat-count';
import {DayOfWeekPipe} from '../../../_pipes/day-of-week.pipe';
import {takeUntilDestroyed} from "@angular/core/rxjs-interop";
import {TranslocoDirective} from "@jsverse/transloco";
import {BarChartComponent} from "../../../shared/_charts/bar-chart/bar-chart.component";
import {StatsNoDataComponent} from "../../../common/stats-no-data/stats-no-data.component";

@Component({
    selector: 'app-day-breakdown',
    templateUrl: './day-breakdown.component.html',
    styleUrls: ['./day-breakdown.component.scss'],
    changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [TranslocoDirective, BarChartComponent, StatsNoDataComponent]
})
export class DayBreakdownComponent {
  private readonly destroyRef = inject(DestroyRef);
  private readonly statService = inject(StatisticsService);
  private readonly dayOfWeekPipe = new DayOfWeekPipe();

  userId = input<number>(0);

  private readonly rawData = signal<StatCount<DayOfWeek>[]>([]);

  readonly axisLabels = computed(() =>
    this.rawData().map(d => this.dayOfWeekPipe.transform(d.value))
  );

  readonly data = computed(() =>
    this.rawData().map(d => d.count)
  );

  readonly hasData = computed(() => this.rawData().length > 0);

  constructor() {
    this.statService.getDayBreakdown(this.userId()).pipe(
      takeUntilDestroyed(this.destroyRef)
    ).subscribe(data => this.rawData.set(data));
  }
}
