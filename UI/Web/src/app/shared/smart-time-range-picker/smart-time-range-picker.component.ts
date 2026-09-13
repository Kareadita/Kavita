import {ChangeDetectionStrategy, Component, computed, effect, input, output, signal} from '@angular/core';
import {translate, TranslocoDirective} from "@jsverse/transloco";
import {FormFieldDirective} from "../../_directives/form-field.directive";
import {debounce, form, FormField} from "@angular/forms/signals";

export type TimeRange = {
  startDate: Date | null,
  endDate: Date | null,
}

@Component({
  selector: 'app-smart-time-range-picker',
  standalone: true,
  imports: [TranslocoDirective, FormFieldDirective, FormField],
  templateUrl: './smart-time-range-picker.component.html',
  styleUrl: './smart-time-range-picker.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class SmartTimeRangePickerComponent {

  startYear = input.required<number>();
  locale = input<'server' | 'profile'>('profile');

  timeRangeUpdate = output<TimeRange>();

  formModel = signal<TimeRange>({
    startDate: null,
    endDate: null,
  });
  formGroup = form(this.formModel, path => {
    debounce(path, 300);
  });

  readonly isOpen = signal(false);
  readonly dropDownMode = signal<'all' | 'year' | 'date'>('all');

  readonly displayText = computed(() => {
    const selectedTime = this.formModel();
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
    ).sort((a, b) => b - a);
  })

  constructor() {
    effect(() => {
      const timeRange = this.formModel();
      this.timeRangeUpdate.emit({
        startDate: timeRange.startDate ? new Date(timeRange.startDate) : null,
        endDate: timeRange.endDate ? new Date(timeRange.endDate) : null,
      })
    });
  }

  toggleDropdown() {
    this.isOpen.update(v => !v);
  }

  closeDropdown() {
    this.isOpen.set(false);
    this.dropDownMode.set('all');
  }

  selectForever() {
    this.formModel.set({
      startDate: null,
      endDate: null,
    })
    this.closeDropdown();
  }

  setYearRange(year: number) {
    const startDate = new Date(year, 0, 1); // Jan 1
    const endDate = new Date(year, 11, 31); // Dec 31
    this.formModel.set({
      startDate,
      endDate
    });
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
