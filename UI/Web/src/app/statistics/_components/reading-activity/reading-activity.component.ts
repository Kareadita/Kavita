import {
  ChangeDetectionStrategy,
  Component,
  computed,
  DestroyRef,
  inject,
  input,
  OnInit,
  signal
} from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule } from '@angular/forms';
import { filter, map, Observable, of, shareReplay, switchMap } from 'rxjs';
import { Member } from 'src/app/_models/auth/member';
import { MemberService } from 'src/app/_services/member.service';
import { StatisticsService } from 'src/app/_services/statistics.service';
import { TimePeriods } from '../top-readers/top-readers.component';
import {takeUntilDestroyed} from "@angular/core/rxjs-interop";
import { LineChartModule } from '@swimlane/ngx-charts';
import { AsyncPipe } from '@angular/common';
import {TranslocoDirective} from "@jsverse/transloco";
import {LineChartComponent} from "../../../shared/_charts/line-chart/line-chart.component";
import {MangaFormatPipe} from "../../../_pipes/manga-format.pipe";
import {MangaFormat} from "../../../_models/manga-format";

const dateOptions: Intl.DateTimeFormatOptions = { month: 'short', day: 'numeric' };

interface ProcessedChartData {
  axisLabels: string[];
  legendLabels: string[];
  data: number[][];
  hasData: boolean;
}

interface PagesReadOnADayCount {
  value: string | Date;
  count: number;
  format: MangaFormat;
}

@Component({
    selector: 'app-reading-activity',
    templateUrl: './reading-activity.component.html',
    styleUrls: ['./reading-activity.component.scss'],
    changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ReactiveFormsModule, LineChartModule, AsyncPipe, TranslocoDirective, LineChartComponent]
})
export class ReadingActivityComponent implements OnInit {
  private readonly destroyRef = inject(DestroyRef);
  private readonly statService = inject(StatisticsService);
  private readonly memberService = inject(MemberService);
  private readonly mangaFormatPipe = new MangaFormatPipe();

  userId = input<number>(0);
  isAdmin = input<boolean>(true);
  individualUserMode = input<boolean>(false);

  private rawData = signal<ProcessedChartData>({
    axisLabels: [],
    legendLabels: [],
    data: [],
    hasData: false
  });

  chartData = computed(() => this.rawData());


  view: [number, number] = [0, 400];
  formGroup: FormGroup = new FormGroup({
    'users': new FormControl(-1, []),
    'days': new FormControl(TimePeriods[0].value, []),
  });
  users$: Observable<Member[]> | undefined;
  timePeriods = TimePeriods;

  constructor() {

    this.formGroup.valueChanges.pipe(
      switchMap(() => {
        const userId = this.formGroup.get('users')!.value ?? 0;
        const days = this.formGroup.get('days')!.value ?? TimePeriods[0].value;
        return this.statService.getReadCountByDay(userId, days);
      }),
      map(data => this.transformData(data)),
      takeUntilDestroyed(this.destroyRef),
    ).subscribe(processed => this.rawData.set(processed));
  }

  ngOnInit(): void {
    this.users$ = (this.isAdmin() ? this.memberService.getMembers() : of([])).pipe(
      filter(_ => this.isAdmin()),
      takeUntilDestroyed(this.destroyRef),
      shareReplay());
    this.formGroup.get('users')?.setValue(this.userId(), {emitValue: true});

    if (!this.isAdmin()) {
      this.formGroup.get('users')?.disable();
    }
  }

  private transformData(data: PagesReadOnADayCount[]): ProcessedChartData {
    if (!data || data.length === 0) {
      return { axisLabels: [], legendLabels: [], data: [], hasData: false };
    }

    // 1. Collect all unique dates and sort them
    const uniqueDates = [...new Set(data.map(d => new Date(d.value).getTime()))]
      .sort((a, b) => a - b);

    const axisLabels = uniqueDates.map(ts =>
      new Date(ts).toLocaleDateString('en-US', dateOptions)
    );

    // 2. Determine which formats are present in the data
    const presentFormats = [...new Set(data.map(d => d.format))].sort();
    const legendLabels = presentFormats.map(f => this.mangaFormatPipe.transform(f));

    // 3. Build date-to-index lookup for O(1) access
    const dateIndexMap = new Map<number, number>();
    uniqueDates.forEach((ts, idx) => dateIndexMap.set(ts, idx));

    // 4. Build format-to-index lookup
    const formatIndexMap = new Map<MangaFormat, number>();
    presentFormats.forEach((format, idx) => formatIndexMap.set(format, idx));

    // 5. Initialize 2D array: [format][date] = count
    const chartData: number[][] = presentFormats.map(() =>
      new Array(uniqueDates.length).fill(0)
    );

    // 6. Populate data
    for (const entry of data) {
      const dateTs = new Date(entry.value).getTime();
      const dateIdx = dateIndexMap.get(dateTs);
      const formatIdx = formatIndexMap.get(entry.format);

      if (dateIdx !== undefined && formatIdx !== undefined) {
        chartData[formatIdx][dateIdx] = entry.count;
      }
    }

    return {
      axisLabels,
      legendLabels,
      data: chartData,
      hasData: true
    };
  }
}

