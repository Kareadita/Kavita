import { ChangeDetectionStrategy, ChangeDetectorRef, Component, OnDestroy, OnInit } from '@angular/core';
import { FormGroup, FormControl } from '@angular/forms';
import { Observable, Subject, map, takeUntil, switchMap, shareReplay, tap } from 'rxjs';
import { StatisticsService } from 'src/app/_services/statistics.service';
import { TopReads, TopRead, TopUserRead } from '../../_models/top-reads';

@Component({
  selector: 'app-top-reads-by-user',
  templateUrl: './top-reads-by-user.component.html',
  styleUrls: ['./top-reads-by-user.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class TopReadsByUserComponent implements OnInit, OnDestroy {

  formGroup: FormGroup;
  timePeriods: Array<{title: string, value: number}> = [{title: 'Last 7 Days', value: 7}, {title: 'Last 30 Days', value: 30}, {title: 'Last 90 Days', value: 90}, {title: 'Last Year', value: 365}, {title: 'All Time', value: 0}];

  users$: Observable<TopUserRead[]>;
  private readonly onDestroy = new Subject<void>();
  
  constructor(private statsService: StatisticsService, private readonly cdRef: ChangeDetectorRef) { 
    this.formGroup = new FormGroup({
      'days': new FormControl(this.timePeriods[0].value, []),
    });

    this.users$ = this.statsService.getTopUsers().pipe(
      //switchMap(_ => this.statsService.getTopUsers()),
      takeUntil(this.onDestroy),
      shareReplay(),
      tap(d => console.log('top reads by user: ', d))
    );
    
  }

  ngOnInit(): void {
    // Needed so that other pipes work
    //this.users$.subscribe();
  }

  ngOnDestroy(): void {
    this.onDestroy.next();
    this.onDestroy.complete();
  }

}
