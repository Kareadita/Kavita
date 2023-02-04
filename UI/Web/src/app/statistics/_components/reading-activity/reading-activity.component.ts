import { ChangeDetectionStrategy, Component, Input, OnDestroy, OnInit } from '@angular/core';
import { FormControl, FormGroup } from '@angular/forms';
import { filter, map, Observable, of, shareReplay, Subject, switchMap, takeUntil } from 'rxjs';
import { MangaFormatPipe } from 'src/app/pipe/manga-format.pipe';
import { Member } from 'src/app/_models/auth/member';
import { MemberService } from 'src/app/_services/member.service';
import { StatisticsService } from 'src/app/_services/statistics.service';
import { PieDataItem } from '../../_models/pie-data-item';
import { TimePeriods } from '../top-readers/top-readers.component';

const options: Intl.DateTimeFormatOptions  = { month: "short", day: "numeric" };
const mangaFormatPipe = new MangaFormatPipe();

@Component({
  selector: 'app-reading-activity',
  templateUrl: './reading-activity.component.html',
  styleUrls: ['./reading-activity.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class ReadingActivityComponent implements OnInit, OnDestroy {
  /**
   * Only show for one user
   */
  @Input() userId: number = 0;
  @Input() isAdmin: boolean = true;
  @Input() individualUserMode: boolean = false;

  view: [number, number] = [0, 400];
  formGroup: FormGroup = new FormGroup({
    'users': new FormControl(-1, []),
    'days': new FormControl(TimePeriods[0].value, []),
  });
  users$: Observable<Member[]> | undefined;
  data$: Observable<Array<PieDataItem>>;
  timePeriods = TimePeriods;
  private readonly onDestroy = new Subject<void>();

  constructor(private statService: StatisticsService, private memberService: MemberService) {
    this.data$ = this.formGroup.valueChanges.pipe(      
      switchMap(_ => this.statService.getReadCountByDay(this.formGroup.get('users')!.value, this.formGroup.get('days')!.value)),
      map(data => {
        const gList = data.reduce((formats, entry) => {
          const formatTranslated = mangaFormatPipe.transform(entry.format);
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
      takeUntil(this.onDestroy),
      shareReplay(),
    );
    
    this.data$.subscribe();
  }

  ngOnInit(): void {
    this.users$ = (this.isAdmin ? this.memberService.getMembers() : of([])).pipe(filter(_ => this.isAdmin), takeUntil(this.onDestroy), shareReplay());
    this.formGroup.get('users')?.setValue(this.userId, {emitValue: true});

    if (!this.isAdmin) {
      this.formGroup.get('users')?.disable();
    }
  }

  ngOnDestroy(): void {
    this.onDestroy.next();
    this.onDestroy.complete();
  }

}

