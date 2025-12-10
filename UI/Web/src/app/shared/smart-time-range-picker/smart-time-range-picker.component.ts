import {ChangeDetectionStrategy, Component, computed, inject, OnInit, output, signal} from '@angular/core';
import {FormControl, FormGroup, ReactiveFormsModule} from "@angular/forms";
import {tap} from "rxjs";
import {CommonModule} from '@angular/common';
import {toSignal} from "@angular/core/rxjs-interop";
import {translate, TranslocoDirective} from "@jsverse/transloco";
import {SettingsService} from "../../admin/settings.service";

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
  imports: [ReactiveFormsModule, CommonModule, TranslocoDirective],
  templateUrl: './smart-time-range-picker.component.html',
  styleUrl: './smart-time-range-picker.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class SmartTimeRangePickerComponent implements OnInit {

  private settingsService = inject(SettingsService);

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
      return translate('smart-time-picker.during-entire-life');
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
  readonly yearOptions = signal<number[]>([]);

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

  ngOnInit() {
    this.settingsService.getFirstInstallDate().pipe(
      tap(installDate => {
        const installYear = new Date(installDate).getFullYear();
        const amountOfYears = new Date().getFullYear() - installYear + 1;

        this.yearOptions.set(Array.from(
          {length: amountOfYears},
          (_, i) => installYear + i,
        ));
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
