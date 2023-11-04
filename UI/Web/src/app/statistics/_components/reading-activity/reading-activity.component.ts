import {ChangeDetectionStrategy, ChangeDetectorRef, Component, DestroyRef, inject, Input, OnInit} from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule } from '@angular/forms';
import { filter, map, Observable, of, shareReplay, switchMap } from 'rxjs';
import { MangaFormatPipe } from 'src/app/pipe/manga-format.pipe';
import { Member } from 'src/app/_models/auth/member';
import { MemberService } from 'src/app/_services/member.service';
import { StatisticsService } from 'src/app/_services/statistics.service';
import { PieDataItem } from '../../_models/pie-data-item';
import { TimePeriods } from '../top-readers/top-readers.component';
import {takeUntilDestroyed} from "@angular/core/rxjs-interop";
import { LineChartModule } from '@swimlane/ngx-charts';
import { NgIf, NgFor, AsyncPipe } from '@angular/common';
import {TranslocoDirective, TranslocoService} from "@ngneat/transloco";
import {UtcToLocalTimePipe} from "../../../pipe/utc-to-local-time.pipe";
import {tap} from "rxjs/operators";
import {MangaFormat} from "../../../_models/manga-format";

const options: Intl.DateTimeFormatOptions  = { month: "short", day: "numeric" };

interface DayPoint {
  count: number, format: MangaFormat
}
interface DayGroup {
  date: string, values: Array<DayPoint>
}

@Component({
  selector: 'app-reading-activity',
  templateUrl: './reading-activity.component.html',
  styleUrls: ['./reading-activity.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
  standalone: true,
  imports: [ReactiveFormsModule, NgIf, NgFor, LineChartModule, AsyncPipe, TranslocoDirective, MangaFormatPipe]
})
export class ReadingActivityComponent implements OnInit {
  /**
   * Only show for one user
   */
  @Input() userId: number = 0;
  @Input() isAdmin: boolean = true;
  @Input() individualUserMode: boolean = false;

  private readonly utcDatePipe = new UtcToLocalTimePipe();
  private readonly destroyRef = inject(DestroyRef);
  private readonly translocoService = inject(TranslocoService);
  private readonly cdRef = inject(ChangeDetectorRef);

  view: [number, number] = [0, 400];
  formGroup: FormGroup = new FormGroup({
    'users': new FormControl(-1, []),
    'days': new FormControl(TimePeriods[0].value, []),
  });
  users$: Observable<Member[]> | undefined;
  data$: Observable<Array<PieDataItem>>;
  data2$: Observable<Array<any>>;
  timePeriods = TimePeriods;
  max: number = 0;
  mangaFormatPipe = new MangaFormatPipe(this.translocoService);




  constructor(private statService: StatisticsService, private memberService: MemberService) {
    this.data2$ = this.formGroup.valueChanges.pipe(
      switchMap(_ => this.statService.getReadCountByDay(this.formGroup.get('users')!.value, this.formGroup.get('days')!.value)),
      // tap(data => {
      //   this.max = data.reduce((acc, day) => Math.max(acc, day.value), 0);
      //   console.log('max: ', this.max);
      //   this.cdRef.markForCheck();
      // }),
      map(data => {

        // We need the data to be grouped by date
        const groupedData: any = {};

        data.forEach((item) => {
          const date = new Date(item.value).toLocaleDateString("en-US", options);
          if (!groupedData[date]) {
            groupedData[date] = [];
          }
          this.max = Math.max(this.max, item.count);
          groupedData[date].push({
            count: item.count,
            format: item.format,
          });

        });

        // Convert the grouped data object into an array of objects
        const result = Object.entries(groupedData)
        .map(([date, values]) => ({
          date,
          values,
        }));

        console.log(result);

        return result;
      }),
      takeUntilDestroyed(this.destroyRef),
      shareReplay(),
    );


    this.data$ = this.formGroup.valueChanges.pipe(
      switchMap(_ => this.statService.getReadCountByDay(this.formGroup.get('users')!.value, this.formGroup.get('days')!.value)),
      map(data => {
        const gList = data.reduce((formats, entry) => {
          const formatTranslated = this.mangaFormatPipe.transform(entry.format);
          if (!formats[formatTranslated]) {
            formats[formatTranslated] = {
              name: formatTranslated,
              value: 0,
              series: []
            };
          }
          formats[formatTranslated].series.push({name: new Date(entry.value).toLocaleDateString("en-US", options), value: entry.count});

          return formats;
        }, {});
        return Object.keys(gList).map(format => {
          return {name: format, value: 0, series: gList[format].series}
        });
      }),
      takeUntilDestroyed(this.destroyRef),
      shareReplay(),
    );

    this.data$.subscribe();
  }

  ngOnInit(): void {
    this.users$ = (this.isAdmin ? this.memberService.getMembers() : of([])).pipe(filter(_ => this.isAdmin), takeUntilDestroyed(this.destroyRef), shareReplay());
    this.formGroup.get('users')?.setValue(this.userId, {emitValue: true});

    if (!this.isAdmin) {
      this.formGroup.get('users')?.disable();
    }
  }
}

