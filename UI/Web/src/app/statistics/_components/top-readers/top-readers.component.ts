import { ChangeDetectionStrategy, ChangeDetectorRef, Component, OnDestroy, OnInit } from '@angular/core';
import { FormGroup, FormControl } from '@angular/forms';
import { Observable, Subject, takeUntil, switchMap, shareReplay } from 'rxjs';
import { StatisticsService } from 'src/app/_services/statistics.service';
import { TopUserRead } from '../../_models/top-reads';

@Component({
  selector: 'app-top-readers',
  templateUrl: './top-readers.component.html',
  styleUrls: ['./top-readers.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class TopReadersComponent implements OnInit, OnDestroy {

  formGroup: FormGroup;
  timePeriods: Array<{title: string, value: number}> = [{title: 'Last 7 Days', value: 7}, {title: 'Last 30 Days', value: 30}, {title: 'Last 90 Days', value: 90}, {title: 'Last Year', value: 365}, {title: 'All Time', value: 0}];

  users$: Observable<TopUserRead[]>;
  private readonly onDestroy = new Subject<void>();
  
  constructor(private statsService: StatisticsService, private readonly cdRef: ChangeDetectorRef) { 
    this.formGroup = new FormGroup({
      'days': new FormControl(this.timePeriods[0].value, []),
    });

    this.users$ = this.formGroup.valueChanges.pipe(
      switchMap(_ => this.statsService.getTopUsers(this.formGroup.get('days')?.value as number)),
      takeUntil(this.onDestroy),
      shareReplay(),
    );
  }

  ngOnInit(): void {
    // Needed so that other pipes work
    this.users$.subscribe();
    this.formGroup.get('days')?.setValue(this.timePeriods[0].value, {emitEvent: true});
  }

  ngOnDestroy(): void {
    this.onDestroy.next();
    this.onDestroy.complete();
  }

}
