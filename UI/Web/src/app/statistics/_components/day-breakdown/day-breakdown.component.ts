import {ChangeDetectionStrategy, Component, DestroyRef, inject, Input, OnInit} from '@angular/core';
import {FormControl} from '@angular/forms';
import { BarChartModule } from '@swimlane/ngx-charts';
import {map, Observable} from 'rxjs';
import {DayOfWeek, StatisticsService} from 'src/app/_services/statistics.service';
import {PieDataItem} from '../../_models/pie-data-item';
import {StatCount} from '../../_models/stat-count';
import {DayOfWeekPipe} from '../../_pipes/day-of-week.pipe';
import {takeUntilDestroyed} from "@angular/core/rxjs-interop";
import {AsyncPipe, NgForOf, NgIf} from '@angular/common';
import {TranslocoDirective} from "@ngneat/transloco";

@Component({
    selector: 'app-day-breakdown',
    templateUrl: './day-breakdown.component.html',
    styleUrls: ['./day-breakdown.component.scss'],
    changeDetection: ChangeDetectionStrategy.OnPush,
    standalone: true,
  imports: [BarChartModule, AsyncPipe, TranslocoDirective, NgForOf, NgIf]
})
export class DayBreakdownComponent implements OnInit {

  @Input() userId = 0;
  view: [number, number] = [0,0];
  showLegend: boolean = true;

  formControl: FormControl = new FormControl(true, []);
  dayBreakdown$!: Observable<Array<PieDataItem>>;
  private readonly destroyRef = inject(DestroyRef);

  constructor(private statService: StatisticsService) {}

  ngOnInit() {
    const dayOfWeekPipe = new DayOfWeekPipe();
    this.dayBreakdown$ = this.statService.getDayBreakdown(this.userId).pipe(
      map((data: Array<StatCount<DayOfWeek>>) => {
        return data.map(d => {
          return {name: dayOfWeekPipe.transform(d.value), value: d.count};
        })
      }),
      takeUntilDestroyed(this.destroyRef)
    );
  }

}
