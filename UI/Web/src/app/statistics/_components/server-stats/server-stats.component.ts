import { ChangeDetectionStrategy, Component, OnInit } from '@angular/core';
import { DownloadService } from 'src/app/shared/_services/download.service';
import { StatisticsService } from 'src/app/_services/statistics.service';
import { FileExtensionBreakdown } from '../../_models/file-breakdown';

@Component({
  selector: 'app-server-stats',
  templateUrl: './server-stats.component.html',
  styleUrls: ['./server-stats.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class ServerStatsComponent implements OnInit {


  constructor(private statService: StatisticsService) {
  }

  ngOnInit(): void {
  }

  

}
