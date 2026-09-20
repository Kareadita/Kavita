import {inject, Injectable, signal} from '@angular/core';
import {NavigationStart, Router} from '@angular/router';
import {filter} from 'rxjs';

@Injectable({
  providedIn: 'root'
})
export class ToggleService {

  private _toggleState = signal(false);
  public readonly toggleState = this._toggleState.asReadonly();

  constructor() {
    const router = inject(Router);

    router.events
    .pipe(filter(event => event instanceof NavigationStart))
    .subscribe((event) => {
      this._toggleState.set(false);
    });
    this._toggleState.set(false);
  }

  toggle() {
    this._toggleState.update(x => !x);
  }

  set(state: boolean) {
    this._toggleState.set(state);
  }
}
