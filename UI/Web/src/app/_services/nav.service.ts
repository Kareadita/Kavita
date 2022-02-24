import { Injectable } from '@angular/core';
import { ReplaySubject, take } from 'rxjs';
import { UtilityService } from '../shared/_services/utility.service';

@Injectable({
  providedIn: 'root'
})
export class NavService {

  public localStorageSideNavKey = 'kavita--sidenav--collapsed';

  private navbarVisibleSource = new ReplaySubject<boolean>(1);
  navbarVisible$ = this.navbarVisibleSource.asObservable();

  private sidenavVisibleSource = new ReplaySubject<boolean>(1);
  sideNavVisible$ = this.sidenavVisibleSource.asObservable();

  private darkMode: boolean = true;
  private darkModeSource = new ReplaySubject<boolean>(1);
  darkMode$ = this.darkModeSource.asObservable();

  constructor(private utilityService: UtilityService) {
    this.showNavBar();
    // TODO: Once we refactor sidenav to have some sticking out, we can use localstorage instead of this
    this.sidenavVisibleSource.next(false);
  }
 
  showNavBar() {
    this.navbarVisibleSource.next(true);
  }

  hideNavBar() {
    this.navbarVisibleSource.next(false);
  }

  toggleSideNav() {
    this.sidenavVisibleSource.pipe(take(1)).subscribe(val => {
      if (val === undefined) val = false;
      const newVal = !(val || false);
      this.sidenavVisibleSource.next(newVal);
      localStorage.setItem(this.localStorageSideNavKey, newVal + '');
    });
  }

  showSideNav(supressSaveState: boolean = true) {
    this.sidenavVisibleSource.next(true);
    if (supressSaveState) return;
    localStorage.setItem(this.localStorageSideNavKey, true + '');
  }

  hideSideNav(supressSaveState: boolean = true) {
    this.sidenavVisibleSource.next(false);
    if (supressSaveState) return;
    localStorage.setItem(this.localStorageSideNavKey, false + '');
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
