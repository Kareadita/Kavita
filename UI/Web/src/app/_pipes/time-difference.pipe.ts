import {ChangeDetectorRef, inject, NgZone, OnDestroy, Pipe, PipeTransform} from '@angular/core';
import {TranslocoService} from "@jsverse/transloco";

/**
 * Like the TimeAgoPipe but allowing future values too
 */

@Pipe({
  name: 'timeDifference',
  pure: false,
  standalone: true
})
export class TimeDifferencePipe implements PipeTransform, OnDestroy {
  private readonly changeDetectorRef = inject(ChangeDetectorRef);
  private readonly ngZone = inject(NgZone);
  private readonly translocoService = inject(TranslocoService);

  private timer: number | null = null;

  transform(value: string | Date | number | null) {
    if (value === '' || value === null || value === undefined || (typeof value === 'string' && value.split('T')[0] === '0001-01-01')) {
      return this.translocoService.translate('time-difference-pipe.never');
    }

    this.removeTimer();
    const d = new Date(value);
    const now = new Date();
    const diffMs = d.getTime() - now.getTime();
    const seconds = Math.round(Math.abs(diffMs / 1000));
    const isFuture = diffMs > 0;
    const timeToUpdate = Number.isNaN(seconds) ? 1000 : this.getSecondsUntilUpdate(seconds) * 1000;

    this.timer = this.ngZone.runOutsideAngular(() => {
      if (typeof window !== 'undefined') {
        return window.setTimeout(() => {
          this.ngZone.run(() => this.changeDetectorRef.markForCheck());
        }, timeToUpdate);
      }
      return null;
    });

    const minutes = Math.round(Math.abs(seconds / 60));
    const hours = Math.round(Math.abs(minutes / 60));
    const days = Math.round(Math.abs(hours / 24));
    const months = Math.round(Math.abs(days / 30.416));
    const years = Math.round(Math.abs(days / 365));

    if (Number.isNaN(seconds)) {
      return '';
    }

    const suffix = isFuture ? 'in' : 'ago';

    if (seconds <= 45) {
      return this.translocoService.translate('time-difference-pipe.just-now');
    }
    if (seconds <= 90) {
      return this.translocoService.translate(`time-difference-pipe.min.${suffix}`);
    }
    if (minutes <= 45) {
      return this.translocoService.translate(`time-difference-pipe.mins.${suffix}`, {value: minutes});
    }
    if (minutes <= 90) {
      return this.translocoService.translate(`time-difference-pipe.hour.${suffix}`);
    }
    if (hours <= 22) {
      return this.translocoService.translate(`time-difference-pipe.hours.${suffix}`, {value: hours});
    }
    if (hours <= 36) {
      return this.translocoService.translate(`time-difference-pipe.day.${suffix}`);
    }
    if (days <= 25) {
      return this.translocoService.translate(`time-difference-pipe.days.${suffix}`, {value: days});
    }
    if (days <= 45) {
      return this.translocoService.translate(`time-difference-pipe.month.${suffix}`);
    }
    if (days <= 345) {
      return this.translocoService.translate(`time-difference-pipe.months.${suffix}`, {value: months});
    }
    if (days <= 545) {
      return this.translocoService.translate(`time-difference-pipe.year.${suffix}`);
    }
    return this.translocoService.translate(`time-difference-pipe.years.${suffix}`, {value: years});
  }

  ngOnDestroy(): void {
    this.removeTimer();
  }

  private removeTimer() {
    if (this.timer) {
      window.clearTimeout(this.timer);
      this.timer = null;
    }
  }

  private getSecondsUntilUpdate(seconds: number) {
    const min = 60;
    const hr = min * 60;
    const day = hr * 24;
    if (seconds < min) {
      return 2;
    } else if (seconds < hr) {
      return 30;
    } else if (seconds < day) {
      return 300;
    } else {
      return 3600;
    }
  }
}
