import {ChangeDetectionStrategy, Component, computed, input, output, signal} from '@angular/core';
import {FormControl, FormGroup, ReactiveFormsModule} from "@angular/forms";
import {tap} from "rxjs";
import {toSignal} from "@angular/core/rxjs-interop";
import {translate, TranslocoDirective} from "@jsverse/transloco";

export type TimeRangeFormGroup = FormGroup<{
  startDate: FormControl<Date | null>,
  endDate: FormControl<Date | null>,
}>

export type TimeRange = {
  startDate: Date | null,
  endDate: Date | null,
}

@Component({
  selector: 'app-smart-time-range-picker',
  standalone: true,
  imports: [ReactiveFormsModule, TranslocoDirective],
  templateUrl: './smart-time-range-picker.component.html',
  styleUrl: './smart-time-range-picker.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class SmartTimeRangePickerComponent {

  startYear = input.required<number>();
  locale = input<'server' | 'profile'>('profile');

  timeRangeUpdate = output<TimeRange>();

  readonly formGroup: TimeRangeFormGroup = new FormGroup({
    startDate: new FormControl<Date | null>(null),
    endDate: new FormControl<Date | null>(null),
  });

  readonly isOpen = signal(false);
  readonly dropDownMode = signal<'all' | 'year' | 'date'>('all');

  readonly selectedTime = toSignal(this.formGroup.valueChanges,
    { initialValue: {startDate: null, endDate: null} });

  readonly displayText = computed(() => {
    const selectedTime = this.selectedTime();
    const start = selectedTime.startDate
    const end = selectedTime.endDate;

    if (!start && !end) {
      return translate('smart-time-picker.during-entire-life-' + this.locale() );
    }

    if (start && end) {
      const startDate = new Date(start);
      const endDate = new Date(end);

      if (startDate.getMonth() === 0 && startDate.getDate() === 1 &&
        endDate.getMonth() === 11 && endDate.getDate() === 31 &&
        startDate.getFullYear() === endDate.getFullYear()) {
        return translate('smart-time-picker.during-year', {year: startDate.getFullYear()});
      }

      return translate('smart-time-picker.during-from-to', {startYear: this.formatDate(startDate), endYear: this.formatDate(endDate)});
    }

    return translate('smart-time-picker.during-select');
  });
  readonly yearOptions = computed(() => {
    const startYear = this.startYear();
    const amountOfYears = new Date().getFullYear() - startYear + 1;

    return Array.from(
      {length: amountOfYears},
      (_, i) => startYear + i,
    );
  })

  constructor() {
    this.formGroup.valueChanges.pipe(
      tap(obj => {
        this.timeRangeUpdate.emit({
          startDate: obj.startDate ? new Date(obj.startDate) : null,
          endDate: obj.endDate ? new Date(obj.endDate) : null,
        });
      })
    ).subscribe();
  }

  toggleDropdown() {
    this.isOpen.update(v => !v);
  }

  closeDropdown() {
    this.isOpen.set(false);
    this.dropDownMode.set('all');
  }

  selectForever() {
    this.formGroup.setValue({ startDate: null, endDate: null });
    this.closeDropdown();
  }

  setYearRange(year: number) {
    const startDate = new Date(year, 0, 1); // Jan 1
    const endDate = new Date(year, 11, 31); // Dec 31
    this.formGroup.setValue({ startDate, endDate });
    this.closeDropdown();
  }

  private formatDate(date: Date): string {
    const options: Intl.DateTimeFormatOptions = {
      year: 'numeric',
      month: 'short',
      day: 'numeric'
    };
    return date.toLocaleDateString('en-US', options);
  }
}
