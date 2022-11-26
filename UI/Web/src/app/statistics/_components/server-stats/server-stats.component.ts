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

  size: string = '';

  constructor(private statService: StatisticsService) {
    //this.statService.getTotalSize().subscribe(s => this.size = DownloadService.humanFileSize(s));
    this.statService.getFileBreakdown().subscribe(s => {
      console.log('File breakdown: ', s);
    });
  }

  ngOnInit(): void {
  }

  

}
