import { ChangeDetectionStrategy, Component, OnDestroy, OnInit } from '@angular/core';
import { Observable, Subject, takeUntil } from 'rxjs';
import { DownloadService } from 'src/app/shared/_services/download.service';
import { StatisticsService } from 'src/app/_services/statistics.service';
import { FileExtensionBreakdown } from '../../_models/file-breakdown';
import { ServerStatistics } from '../../_models/server-statistics';

@Component({
  selector: 'app-server-stats',
  templateUrl: './server-stats.component.html',
  styleUrls: ['./server-stats.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class ServerStatsComponent implements OnInit, OnDestroy {

  stats$!: Observable<ServerStatistics>;
  private readonly onDestroy = new Subject<void>();

  constructor(private statService: StatisticsService) {
    this.stats$ = this.statService.getServerStatistics().pipe(takeUntil(this.onDestroy));
  }

  ngOnInit(): void {
  }

  ngOnDestroy(): void {
    this.onDestroy.next();
    this.onDestroy.complete();
  }

  

}
