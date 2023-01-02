import { ChangeDetectionStrategy, ChangeDetectorRef, Component, OnDestroy, OnInit } from '@angular/core';
import { map, Observable, shareReplay, Subject, takeUntil } from 'rxjs';
import { FilterUtilitiesService } from 'src/app/shared/_services/filter-utilities.service';
import { UserReadStatistics } from 'src/app/statistics/_models/user-read-statistics';
import { StatisticsService } from 'src/app/_services/statistics.service';
import { ReadHistoryEvent } from '../../_models/read-history-event';
import { MemberService } from 'src/app/_services/member.service';
import { AccountService } from 'src/app/_services/account.service';
import { PieDataItem } from '../../_models/pie-data-item';
import { LibraryService } from 'src/app/_services/library.service';
import { PercentPipe } from '@angular/common';

@Component({
  selector: 'app-user-stats',
  templateUrl: './user-stats.component.html',
  styleUrls: ['./user-stats.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class UserStatsComponent implements OnInit, OnDestroy {

  userId: number | undefined = undefined;
  userStats$!: Observable<UserReadStatistics>;
  readSeries$!: Observable<ReadHistoryEvent[]>;
  isAdmin$: Observable<boolean>;
  precentageRead$!: Observable<PieDataItem[]>;

  private readonly onDestroy = new Subject<void>();

  constructor(private readonly cdRef: ChangeDetectorRef, private statService: StatisticsService, 
    private filterService: FilterUtilitiesService, private accountService: AccountService, private memberService: MemberService,
    private libraryService: LibraryService) { 
      this.isAdmin$ = this.accountService.currentUser$.pipe(takeUntil(this.onDestroy), map(u => {
        if (!u) return false;
        return this.accountService.hasAdminRole(u);
      }));

    }

  ngOnInit(): void {
    const filter = this.filterService.createSeriesFilter();
    filter.readStatus = {read: true, notRead: false, inProgress: true};
    this.memberService.getMember().subscribe(me => {
      this.userId = me.id;
      this.cdRef.markForCheck();
      
      this.userStats$ = this.statService.getUserStatistics(this.userId).pipe(takeUntil(this.onDestroy), shareReplay());
      this.readSeries$ = this.statService.getReadingHistory(this.userId).pipe(
        takeUntil(this.onDestroy), 
      );
      
      const pipe = new PercentPipe('en-US');
      this.libraryService.getLibraryNames().subscribe(names => {
        this.precentageRead$ = this.userStats$.pipe(takeUntil(this.onDestroy), map(d => d.percentReadPerLibrary.map(l => {
          return {name: names[l.count], value: parseFloat((pipe.transform(l.value, '1.1-1') || '0').replace('%', ''))};
        }).sort((a: PieDataItem, b: PieDataItem) => b.value - a.value)));
      })
      
    });
    
  }

  ngOnDestroy(): void {
    this.onDestroy.next();
    this.onDestroy.complete();
  }

}
