import {ChangeDetectorRef, NgZone, OnDestroy, Pipe, PipeTransform} from '@angular/core';
import {TranslocoService} from "@jsverse/transloco";

/**
 * MIT License

Copyright (c) 2016 Andrew Poyntz

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.

This code was taken from https://github.com/AndrewPoyntz/time-ago-pipe/blob/master/time-ago.pipe.ts
and modified
 */

@Pipe({
  name: 'timeAgo',
  pure: false,
  standalone: true
})
export class TimeAgoPipe implements PipeTransform, OnDestroy {

	private timer: number | null = null;
	constructor(private readonly changeDetectorRef: ChangeDetectorRef, private ngZone: NgZone,
              private translocoService: TranslocoService) {}

	transform(value: string) {

    if (value === '' || value === null || value === undefined || value.split('T')[0] === '0001-01-01')  {
      return this.translocoService.translate('time-ago-pipe.never');
    }

		this.removeTimer();
		const d = new Date(value);
		const now = new Date();
		const seconds = Math.round(Math.abs((now.getTime() - d.getTime()) / 1000));
		const timeToUpdate = (Number.isNaN(seconds)) ? 1000 : this.getSecondsUntilUpdate(seconds) * 1000;

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
		const months = Math.round(Math.abs(days/30.416));
		const years = Math.round(Math.abs(days/365));

		if (Number.isNaN(seconds)){
			return '';
		}

		if (seconds <= 45) {
				return this.translocoService.translate('time-ago-pipe.just-now');
    }
		if (seconds <= 90) {
      return this.translocoService.translate('time-ago-pipe.min-ago');
    }
		if (minutes <= 45) {
      return this.translocoService.translate('time-ago-pipe.mins-ago', {value: minutes});
    }
		if (minutes <= 90) {
      return this.translocoService.translate('time-ago-pipe.hour-ago');
    }
		if (hours <= 22) {
      return this.translocoService.translate('time-ago-pipe.hours-ago', {value: hours});
    }
		if (hours <= 36) {
      return this.translocoService.translate('time-ago-pipe.day-ago');
    }
		if (days <= 25) {
      return this.translocoService.translate('time-ago-pipe.days-ago', {value: days});
    }
		if (days <= 45) {
      return this.translocoService.translate('time-ago-pipe.month-ago');
    }
		if (days <= 345) {
      return this.translocoService.translate('time-ago-pipe.months-ago', {value: months});
    }
		if (days <= 545) {
      return this.translocoService.translate('time-ago-pipe.year-ago');
    }
    return this.translocoService.translate('time-ago-pipe.years-ago', {value: years});
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

	private getSecondsUntilUpdate(seconds:number) {
		const min = 60;
		const hr = min * 60;
		const day = hr * 24;
		if (seconds < min) { // less than 1 min, update every 2 secs
			return 2;
		} else if (seconds < hr) { // less than an hour, update every 30 secs
			return 30;
		} else if (seconds < day) { // less then a day, update every 5 mins
			return 300;
		} else { // update every hour
			return 3600;
		}
	}

}
