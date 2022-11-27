import { ChangeDetectionStrategy, ChangeDetectorRef, Component, OnDestroy, OnInit } from '@angular/core';
import { FormControl, FormGroup } from '@angular/forms';
import { combineLatest, from, map, Observable, of, shareReplay, startWith, Subject, switchMap, takeUntil, tap } from 'rxjs';
import { ImageService } from 'src/app/_services/image.service';
import { MemberService } from 'src/app/_services/member.service';
import { StatisticsService } from 'src/app/_services/statistics.service';
import { PieDataItem } from '../../_models/pie-data-item';
import { TopRead, TopReads } from '../../_models/top-reads';

@Component({
  selector: 'app-top-reads-by-extension',
  templateUrl: './top-reads-by-extension.component.html',
  styleUrls: ['./top-reads-by-extension.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class TopReadsByExtensionComponent implements OnInit, OnDestroy {

  fakeData: Array<PieDataItem> = [{name: 'Accel World', value: 1, extra: {users: 1, seriesId: 1}}, {name: 'Mob Psycho 100', value: 1, extra: {users: 3, seriesId: 3}}];
  formGroup: FormGroup;
  memberNames$: Observable<string[]>;
  timePeriods: Array<{title: string, value: number}> = [{title: 'Last 7 Days', value: 7}, {title: 'Last 30 Days', value: 30}, {title: 'Last 90 Days', value: 90}, {title: 'Last Year', value: 365}, {title: 'All Time', value: 0}];

  rawData$: Observable<TopReads>;
  mangaReads$: Observable<TopRead[]>;
  comicReads$: Observable<TopRead[]>;
  bookReads$: Observable<TopRead[]>;
  private readonly onDestroy = new Subject<void>();
  
  constructor(private memberService: MemberService, public imageService: ImageService, 
    private statsService: StatisticsService, private readonly cdRef: ChangeDetectorRef) { 
    this.formGroup = new FormGroup({
      'member': new FormControl('', []),
      'days': new FormControl(this.timePeriods[0].value, []),
    });
    this.memberNames$ = this.memberService.getMemberNames().pipe(
      map(names => {
        return ['All users', ...names];
      }),
      takeUntil(this.onDestroy)
    );

    this.rawData$ = this.formGroup.valueChanges.pipe(
      switchMap(_ => this.statsService.getTopReads(this.formGroup.value?.member, this.formGroup.value?.days)),
      takeUntil(this.onDestroy),
      shareReplay()
    );
      
    this.mangaReads$ = this.rawData$.pipe(takeUntil(this.onDestroy), map(data => data.manga));
    this.comicReads$ = this.rawData$.pipe(takeUntil(this.onDestroy), map(data => data.comics));
    this.bookReads$ = this.rawData$.pipe(takeUntil(this.onDestroy), map(data => data.books));
  }

  ngOnInit(): void {
    // Needed so that other pipes work
    this.rawData$.subscribe();
    this.formGroup.get('member')?.setValue('All users', {emitEvent: true});
  }

  ngOnDestroy(): void {
    this.onDestroy.next();
    this.onDestroy.complete();
  }

}
