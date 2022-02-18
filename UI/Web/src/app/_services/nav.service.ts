import { Injectable } from '@angular/core';
import { ReplaySubject } from 'rxjs';
import { UtilityService } from '../shared/_services/utility.service';

@Injectable({
  providedIn: 'root'
})
export class NavService {

  private navbarVisibleSource = new ReplaySubject<boolean>(1);
  navbarVisible$ = this.navbarVisibleSource.asObservable();

  private sidenavVisibleSource = new ReplaySubject<boolean>(1);
  sideNavVisible$ = this.sidenavVisibleSource.asObservable();

  private darkMode: boolean = true;
  private darkModeSource = new ReplaySubject<boolean>(1);
  darkMode$ = this.darkModeSource.asObservable();

  constructor(private utilityService: UtilityService) {
    this.showNavBar();
    this.showSideNav();
  }
 
  showNavBar() {
    this.navbarVisibleSource.next(true);
  }

  hideNavBar() {
    this.navbarVisibleSource.next(false);
  }

  showSideNav() {
    this.sidenavVisibleSource.next(true);
  }

  hideSideNav() {
    this.sidenavVisibleSource.next(false);
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
