import { ChangeDetectorRef, NgZone, OnDestroy, Pipe, PipeTransform } from '@angular/core';

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
  pure: false
})
export class TimeAgoPipe implements PipeTransform, OnDestroy {

  private timer: number | null = null;
	constructor(private changeDetectorRef: ChangeDetectorRef, private ngZone: NgZone) {}
	transform(value:string) {
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
			return 'just now';
		}
    if (seconds <= 90) {
			return 'a minute ago';
		}
    if (minutes <= 45) {
			return minutes + ' minutes ago';
		}
    if (minutes <= 90) {
			return 'an hour ago';
		}
    if (hours <= 22) {
			return hours + ' hours ago';
		}
    if (hours <= 36) {
			return 'a day ago';
		}
    if (days <= 25) {
			return days + ' days ago';
		}
    if (days <= 45) {
			return 'a month ago';
		}
    if (days <= 345) {
			return months + ' months ago';
		}
    if (days <= 545) {
			return 'a year ago';
		}
    return years + ' years ago';
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
