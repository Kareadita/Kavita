import { ChangeDetectionStrategy, ChangeDetectorRef, Component, Input, OnDestroy, OnInit, QueryList, ViewChildren } from '@angular/core';
import { map, Observable, of, Subject, takeUntil } from 'rxjs';
import { FilterUtilitiesService } from 'src/app/shared/_services/filter-utilities.service';
import { Series } from 'src/app/_models/series';
import { UserReadStatistics } from 'src/app/statistics/_models/user-read-statistics';
import { SeriesService } from 'src/app/_services/series.service';
import { StatisticsService } from 'src/app/_services/statistics.service';
import { SortableHeader, SortEvent } from 'src/app/_single-module/table/_directives/sortable-header.directive';
import { ReadHistoryEvent } from '../../_models/read-history-event';

type SeriesWithProgress = Series & {progress: number};

@Component({
  selector: 'app-user-stats',
  templateUrl: './user-stats.component.html',
  styleUrls: ['./user-stats.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class UserStatsComponent implements OnInit, OnDestroy {

  @Input() userId!: number;

  @ViewChildren(SortableHeader) headers!: QueryList<SortableHeader<SeriesWithProgress>>;

  userStats$!: Observable<UserReadStatistics>;
  readSeries$!: Observable<ReadHistoryEvent[]>;

  private readonly onDestroy = new Subject<void>();

  constructor(private readonly cdRef: ChangeDetectorRef, private statService: StatisticsService, private seriesService: SeriesService,
    private filterService: FilterUtilitiesService) { }

  ngOnInit(): void {
    const filter = this.filterService.createSeriesFilter();
    filter.readStatus = {read: true, notRead: false, inProgress: true};
    this.userStats$ = this.statService.getUserStatistics(this.userId).pipe(takeUntil(this.onDestroy));
    this.readSeries$ = this.statService.getReadingHistory(this.userId).pipe(
      takeUntil(this.onDestroy), 
    );
  }

  ngOnDestroy(): void {
    this.onDestroy.next();
    this.onDestroy.complete();
  }

  onSort({ column, direction }: SortEvent<SeriesWithProgress>) {
		// resetting other headers
		this.headers.forEach((header) => {
			if (header.sortable !== column) {
				header.direction = '';
			}
		});

		// sorting countries
		// if (direction === '' || column === '') {
		// 	this.countries = COUNTRIES;
		// } else {
		// 	this.countries = [...COUNTRIES].sort((a, b) => {
		// 		const res = compare(a[column], b[column]);
		// 		return direction === 'asc' ? res : -res;
		// 	});
		// }
	}

}
