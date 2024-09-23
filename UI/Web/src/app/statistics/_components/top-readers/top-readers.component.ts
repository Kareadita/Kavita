import {
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component,
  DestroyRef,
  inject,
  OnInit
} from '@angular/core';
import { FormGroup, FormControl, ReactiveFormsModule } from '@angular/forms';
import { Observable, switchMap, shareReplay } from 'rxjs';
import { StatisticsService } from 'src/app/_services/statistics.service';
import { TopUserRead } from '../../_models/top-reads';
import {takeUntilDestroyed} from "@angular/core/rxjs-interop";
import { AsyncPipe } from '@angular/common';
import {TranslocoDirective} from "@jsverse/transloco";
import {CarouselReelComponent} from "../../../carousel/_components/carousel-reel/carousel-reel.component";

export const TimePeriods: Array<{title: string, value: number}> =
  [{title: 'this-week', value: new Date().getDay() || 1},
    {title: 'last-7-days', value: 7},
    {title: 'last-30-days', value: 30},
    {title: 'last-90-days', value: 90},
    {title: 'last-year', value: 365},
    {title: 'all-time', value: 0}];

@Component({
    selector: 'app-top-readers',
    templateUrl: './top-readers.component.html',
    styleUrls: ['./top-readers.component.scss'],
    changeDetection: ChangeDetectionStrategy.OnPush,
    standalone: true,
  imports: [ReactiveFormsModule, AsyncPipe, TranslocoDirective, CarouselReelComponent]
})
export class TopReadersComponent implements OnInit {

  private readonly destroyRef = inject(DestroyRef);

  formGroup: FormGroup;
  timePeriods = TimePeriods;
  users$: Observable<TopUserRead[]>;


  constructor(private statsService: StatisticsService, private readonly cdRef: ChangeDetectorRef) {
    this.formGroup = new FormGroup({
      'days': new FormControl(this.timePeriods[0].value, []),
    });

    this.users$ = this.formGroup.valueChanges.pipe(
      switchMap(_ => this.statsService.getTopUsers(this.formGroup.get('days')?.value as number)),
      takeUntilDestroyed(this.destroyRef),
      shareReplay(),
    );
  }

  ngOnInit(): void {
    // Needed so that other pipes work
    this.users$.subscribe();
    this.formGroup.get('days')?.setValue(this.timePeriods[0].value, {emitEvent: true});
  }

}
