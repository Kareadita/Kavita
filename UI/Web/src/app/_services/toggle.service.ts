import { Injectable } from '@angular/core';
import { NavigationStart, Router } from '@angular/router';
import { filter, ReplaySubject, take } from 'rxjs';

@Injectable({
  providedIn: 'root'
})
export class ToggleService {

  toggleState: boolean = false;


  private toggleStateSource: ReplaySubject<boolean> = new ReplaySubject<boolean>(1);
  public toggleState$ = this.toggleStateSource.asObservable();

  constructor(router: Router) {
    router.events
    .pipe(filter(event => event instanceof NavigationStart))
    .subscribe((event) => {
      this.toggleState = false;
      this.toggleStateSource.next(this.toggleState);
    });
    this.toggleStateSource.next(false);
  }

  toggle() {
    this.toggleState = !this.toggleState;
    this.toggleStateSource.pipe(take(1)).subscribe(state => {
      this.toggleState = !state;
      this.toggleStateSource.next(this.toggleState);
    });
    
  }

  set(state: boolean) {
    this.toggleState = state;
    this.toggleStateSource.next(state);
  }
}
