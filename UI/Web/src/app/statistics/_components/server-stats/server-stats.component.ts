import { ChangeDetectionStrategy, Component, OnDestroy, OnInit } from '@angular/core';
import { map, Observable, shareReplay, Subject, takeUntil } from 'rxjs';
import { DownloadService } from 'src/app/shared/_services/download.service';
import { User } from 'src/app/_models/user';
import { StatisticsService } from 'src/app/_services/statistics.service';
import { FileExtensionBreakdown } from '../../_models/file-breakdown';
import { PieDataItem } from '../../_models/pie-data-item';
import { ServerStatistics } from '../../_models/server-statistics';
import { StatCount } from '../../_models/stat-count';

@Component({
  selector: 'app-server-stats',
  templateUrl: './server-stats.component.html',
  styleUrls: ['./server-stats.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class ServerStatsComponent implements OnInit, OnDestroy {

  releaseYears$!: Observable<Array<PieDataItem>>;
  mostActiveUsers$!: Observable<Array<PieDataItem>>;
  mostActiveLibrary$!: Observable<Array<PieDataItem>>;
  mostActiveSeries$!: Observable<Array<PieDataItem>>;
  recentlyRead$!: Observable<Array<PieDataItem>>;
  stats$!: Observable<ServerStatistics>;
  private readonly onDestroy = new Subject<void>();

  constructor(private statService: StatisticsService) {
    this.stats$ = this.statService.getServerStatistics().pipe(takeUntil(this.onDestroy), shareReplay());
    this.releaseYears$ = this.statService.getTopYears().pipe(takeUntil(this.onDestroy));
    this.mostActiveUsers$ = this.stats$.pipe(
      map(d => d.mostActiveUsers),
      map(userCounts => userCounts.map(count => {
        return {name: count.value.username, value: count.count};
      })),
      takeUntil(this.onDestroy)
    );

    this.mostActiveLibrary$ = this.stats$.pipe(
      map(d => d.mostActiveLibraries),
      map(counts => counts.map(count => {
        return {name: count.value.name, value: count.count};
      })),
      takeUntil(this.onDestroy)
    );

    this.mostActiveSeries$ = this.stats$.pipe(
      map(d => d.mostActiveLibraries),
      map(counts => counts.map(count => {
        return {name: count.value.name, value: count.count};
      })),
      takeUntil(this.onDestroy)
    );

    this.recentlyRead$ = this.stats$.pipe(
      map(d => d.recentlyRead),
      map(counts => counts.map(count => {
        return {name: count.name, value: -1};
      })),
      takeUntil(this.onDestroy)
    );

    
  }

  ngOnInit(): void {
  }

  ngOnDestroy(): void {
    this.onDestroy.next();
    this.onDestroy.complete();
  }

  

}
