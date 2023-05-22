import {ChangeDetectionStrategy, Component, DestroyRef, inject} from '@angular/core';
import {FormControl} from '@angular/forms';
import {LegendPosition} from '@swimlane/ngx-charts';
import {map, Observable} from 'rxjs';
import {DayOfWeek, StatisticsService} from 'src/app/_services/statistics.service';
import {PieDataItem} from '../../_models/pie-data-item';
import {StatCount} from '../../_models/stat-count';
import {DayOfWeekPipe} from '../../_pipes/day-of-week.pipe';
import {takeUntilDestroyed} from "@angular/core/rxjs-interop";

@Component({
  selector: 'app-day-breakdown',
  templateUrl: './day-breakdown.component.html',
  styleUrls: ['./day-breakdown.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class DayBreakdownComponent {

  view: [number, number] = [0,0];
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
  private readonly destroyRef = inject(DestroyRef);

  constructor(private statService: StatisticsService) {
    const dayOfWeekPipe = new DayOfWeekPipe();
    this.dayBreakdown$ = this.statService.getDayBreakdown().pipe(
      map((data: Array<StatCount<DayOfWeek>>) => {
        return data.map(d => {
          return {name: dayOfWeekPipe.transform(d.value), value: d.count};
        })
      }),
      takeUntilDestroyed(this.destroyRef)
    );
  }

}
