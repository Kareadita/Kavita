import { Injectable } from '@angular/core';
import { ReplaySubject, take } from 'rxjs';

@Injectable({
  providedIn: 'root'
})
export class NavService {

  public localStorageSideNavKey = 'kavita--sidenav--collapsed';

  private navbarVisibleSource = new ReplaySubject<boolean>(1);
  navbarVisible$ = this.navbarVisibleSource.asObservable();

  private sidenavVisibleSource = new ReplaySubject<boolean>(1);
  private sidenavRemoveSource = new ReplaySubject<boolean>(1);
  sideNavVisible$ = this.sidenavVisibleSource.asObservable();
  private removeSideNav: boolean = false;
  removeSideNav$ = this.sidenavRemoveSource.asObservable();

  private darkMode: boolean = true;
  private darkModeSource = new ReplaySubject<boolean>(1);
  darkMode$ = this.darkModeSource.asObservable();

  constructor() {
    this.showNavBar();
    const sideNavState = (localStorage.getItem(this.localStorageSideNavKey) === 'true') || false;
    this.sidenavVisibleSource.next(sideNavState);
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


  addSideNav() {
    this.removeSideNav = false;
    this.sidenavRemoveSource.next(this.removeSideNav);
  }

  deleteSideNav() {
    this.removeSideNav = !this.removeSideNav;
    this.sidenavRemoveSource.next(this.removeSideNav);
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
