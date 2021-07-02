import { Injectable } from '@angular/core';
import { ReplaySubject } from 'rxjs';

@Injectable({
  providedIn: 'root'
})
export class NavService {

  private navbarVisibleSource = new ReplaySubject<boolean>(1);
  navbarVisible$ = this.navbarVisibleSource.asObservable();

  private darkMode: boolean = true;
  private darkModeSource = new ReplaySubject<boolean>(1);
  darkMode$ = this.darkModeSource.asObservable();

  constructor() {
    this.showNavBar();
  }

  showNavBar() {
    this.navbarVisibleSource.next(true);
  }

  hideNavBar() {
    this.navbarVisibleSource.next(false);
  }

  toggleDarkMode() {
    this.darkMode = !this.darkMode;
    this.darkModeSource.next(this.darkMode);
  }

  setDarkMode(mode: boolean) {
    this.darkMode = mode;
    this.darkModeSource.next(this.darkMode);
  }


}
