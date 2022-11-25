import { Component, OnDestroy, OnInit } from '@angular/core';
import { LegendPosition } from '@swimlane/ngx-charts';
import { Observable, map, Subject, takeUntil } from 'rxjs';
import { StatisticsService } from 'src/app/_services/statistics.service';

@Component({
  selector: 'app-release-year',
  templateUrl: './release-year.component.html',
  styleUrls: ['./release-year.component.scss']
})
export class ReleaseYearComponent implements OnInit, OnDestroy {

  releaseYears$!: Observable<Array<{name: string, value: number}>>;
  private readonly onDestroy = new Subject<void>();

  view: [number, number] = [700, 400];
  gradient: boolean = true;
  showLegend: boolean = true;
  showLabels: boolean = true;
  isDoughnut: boolean = false;
  legendPosition: LegendPosition = LegendPosition.Right;
  

  colorScheme = {
    domain: ['#5AA454', '#A10A28', '#C7B42C', '#AAAAAA']
  };


  constructor(private statService: StatisticsService) {
    this.releaseYears$ = this.statService.getYearRange().pipe(map(spreads => spreads.map(spread => {
      return {name: spread.releaseYear + '', value: spread.count};
    })), takeUntil(this.onDestroy));
  }

  ngOnInit(): void {
    this.onDestroy.next();
    this.onDestroy.complete();
  }

  ngOnDestroy(): void {
    
  }

  onSelect(data: any): void {
    console.log('Item clicked', data);
  }

  onActivate(data: any): void {
    console.log('Activate', data);
  }

  onDeactivate(data: any): void {
    console.log('Deactivate', data);
  }
}
