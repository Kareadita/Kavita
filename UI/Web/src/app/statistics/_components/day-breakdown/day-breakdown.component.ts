import { Component, OnDestroy } from '@angular/core';
import { FormControl } from '@angular/forms';
import { LegendPosition } from '@swimlane/ngx-charts';
import { Subject, map, takeUntil, Observable } from 'rxjs';
import { DayOfWeek, StatisticsService } from 'src/app/_services/statistics.service';
import { PieDataItem } from '../../_models/pie-data-item';
import { StatCount } from '../../_models/stat-count';
import { DayOfWeekPipe } from '../../_pipes/day-of-week.pipe';

@Component({
  selector: 'app-day-breakdown',
  templateUrl: './day-breakdown.component.html',
  styleUrls: ['./day-breakdown.component.scss']
})
export class DayBreakdownComponent implements OnDestroy {

  private readonly onDestroy = new Subject<void>();

  view: [number, number] = [700, 400];
  gradient: boolean = true;
  showLegend: boolean = true;
  showLabels: boolean = true;
  isDoughnut: boolean = false;
  legendPosition: LegendPosition = LegendPosition.Right;
  colorScheme = {
    domain: ['#5AA454', '#A10A28', '#C7B42C', '#AAAAAA']
  };

  formControl: FormControl = new FormControl(true, []);
  dayBreakdown$!: Observable<Array<PieDataItem>>;

  constructor(private statService: StatisticsService) {
    const dayOfWeekPipe = new DayOfWeekPipe();
    this.dayBreakdown$ = this.statService.getDayBreakdown().pipe(
      map((data: Array<StatCount<DayOfWeek>>) => {
        return data.map(d => {
          return {name: dayOfWeekPipe.transform(d.value), value: d.count};
        })
      }),
      takeUntil(this.onDestroy)
    );
  }

  ngOnDestroy(): void {
    this.onDestroy.next();
    this.onDestroy.complete();
  }

}
