import {ChangeDetectionStrategy, ChangeDetectorRef, Component, DestroyRef, inject, Input, OnInit} from '@angular/core';
import {FormControl} from '@angular/forms';
import { BarChartModule } from '@swimlane/ngx-charts';
import {map, Observable} from 'rxjs';
import {DayOfWeek, StatisticsService} from 'src/app/_services/statistics.service';
import {PieDataItem} from '../../_models/pie-data-item';
import {StatCount} from '../../_models/stat-count';
import {DayOfWeekPipe} from '../../../_pipes/day-of-week.pipe';
import {takeUntilDestroyed} from "@angular/core/rxjs-interop";
import {AsyncPipe} from '@angular/common';
import {TranslocoDirective} from "@jsverse/transloco";
import {tap} from "rxjs/operators";

@Component({
    selector: 'app-day-breakdown',
    templateUrl: './day-breakdown.component.html',
    styleUrls: ['./day-breakdown.component.scss'],
    changeDetection: ChangeDetectionStrategy.OnPush,
    standalone: true,
  imports: [BarChartModule, AsyncPipe, TranslocoDirective]
})
export class DayBreakdownComponent implements OnInit {

  private readonly destroyRef = inject(DestroyRef);
  private readonly cdRef = inject(ChangeDetectorRef);
  private readonly statService = inject(StatisticsService);

  @Input() userId = 0;

  view: [number, number] = [0,0];
  showLegend: boolean = true;
  max: number = 1;

  formControl: FormControl = new FormControl(true, []);
  dayBreakdown$!: Observable<Array<PieDataItem>>;


  ngOnInit() {
    const dayOfWeekPipe = new DayOfWeekPipe();
    this.dayBreakdown$ = this.statService.getDayBreakdown(this.userId).pipe(
      map((data: Array<StatCount<DayOfWeek>>) => {
        return data.map(d => {
          return {name: dayOfWeekPipe.transform(d.value), value: d.count};
        })
      }),
      tap(data => {
        this.max = data.reduce((acc, day) => Math.max(acc, day.value), 0);
        this.cdRef.markForCheck();
      }),
      takeUntilDestroyed(this.destroyRef)
    );
  }

}
