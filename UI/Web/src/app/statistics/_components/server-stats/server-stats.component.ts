import { Component, OnInit } from '@angular/core';
import { map, Observable } from 'rxjs';
import { StatisticsService } from 'src/app/_services/statistics.service';

@Component({
  selector: 'app-server-stats',
  templateUrl: './server-stats.component.html',
  styleUrls: ['./server-stats.component.scss']
})
export class ServerStatsComponent implements OnInit {


  constructor(private statService: StatisticsService) {
    
  }

  ngOnInit(): void {
  }

  

}
