import {ChangeDetectionStrategy, ChangeDetectorRef, Component, DestroyRef, inject, OnInit} from '@angular/core';
import {map, Observable, shareReplay} from 'rxjs';
import {UserReadStatistics} from 'src/app/statistics/_models/user-read-statistics';
import {StatisticsService} from 'src/app/_services/statistics.service';
import {ReadHistoryEvent} from '../../_models/read-history-event';
import {MemberService} from 'src/app/_services/member.service';
import {AccountService} from 'src/app/_services/account.service';
import {PieDataItem} from '../../_models/pie-data-item';
import {LibraryService} from 'src/app/_services/library.service';
import {AsyncPipe, NgIf, PercentPipe} from '@angular/common';
import {takeUntilDestroyed} from "@angular/core/rxjs-interop";
import {StatListComponent} from '../stat-list/stat-list.component';
import {ReadingActivityComponent} from '../reading-activity/reading-activity.component';
import {UserStatsInfoCardsComponent} from '../user-stats-info-cards/user-stats-info-cards.component';
import {TranslocoModule} from "@ngneat/transloco";
import {DayBreakdownComponent} from "../day-breakdown/day-breakdown.component";

@Component({
    selector: 'app-user-stats',
    templateUrl: './user-stats.component.html',
    styleUrls: ['./user-stats.component.scss'],
    changeDetection: ChangeDetectionStrategy.OnPush,
    standalone: true,
    imports: [
        NgIf,
        UserStatsInfoCardsComponent,
        ReadingActivityComponent,
        StatListComponent,
        AsyncPipe,
        TranslocoModule,
        DayBreakdownComponent,
    ],
})
export class UserStatsComponent implements OnInit {

  userId: number | undefined = undefined;
  userStats$!: Observable<UserReadStatistics>;
  readSeries$!: Observable<ReadHistoryEvent[]>;
  isAdmin$: Observable<boolean>;
  percentageRead$!: Observable<PieDataItem[]>;
  private readonly destroyRef = inject(DestroyRef);

  constructor(private readonly cdRef: ChangeDetectorRef, private statService: StatisticsService,
    private accountService: AccountService, private memberService: MemberService,
    private libraryService: LibraryService) {
      this.isAdmin$ = this.accountService.currentUser$.pipe(takeUntilDestroyed(this.destroyRef), map(u => {
        if (!u) return false;
        return this.accountService.hasAdminRole(u);
      }));

    }

  ngOnInit(): void {
    this.memberService.getMember().subscribe(me => {
      this.userId = me.id;
      this.cdRef.markForCheck();

      this.userStats$ = this.statService.getUserStatistics(this.userId).pipe(takeUntilDestroyed(this.destroyRef), shareReplay());
      this.readSeries$ = this.statService.getReadingHistory(this.userId).pipe(
        takeUntilDestroyed(this.destroyRef),
      );

      const pipe = new PercentPipe('en-US');
      this.libraryService.getLibraryNames().subscribe(names => {
        this.percentageRead$ = this.userStats$.pipe(takeUntilDestroyed(this.destroyRef), map(d => d.percentReadPerLibrary.map(l => {
          return {name: names[l.count], value: parseFloat((pipe.transform(l.value, '1.1-1') || '0').replace('%', ''))};
        }).sort((a: PieDataItem, b: PieDataItem) => b.value - a.value)));
      })

    });
  }

}
