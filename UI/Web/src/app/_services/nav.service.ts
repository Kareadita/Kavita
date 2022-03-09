import { Injectable } from '@angular/core';
import { ReplaySubject, take } from 'rxjs';

@Injectable({
  providedIn: 'root'
})
export class NavService {

  public localStorageSideNavKey = 'kavita--sidenav--collapsed';

  private navbarVisibleSource = new ReplaySubject<boolean>(1);
  navbarVisible$ = this.navbarVisibleSource.asObservable();

  private sideNavCollapseSource = new ReplaySubject<boolean>(1);
  sideNavVisible$ = this.sideNavCollapseSource.asObservable();
  private sideNavVisibility: boolean = false;
  private sideNavVisibilitySource = new ReplaySubject<boolean>(1);
  sideNavVisibility$ = this.sideNavVisibilitySource.asObservable();

  private darkMode: boolean = true;
  private darkModeSource = new ReplaySubject<boolean>(1);
  darkMode$ = this.darkModeSource.asObservable();

  constructor() {
    this.showNavBar();
    const sideNavState = (localStorage.getItem(this.localStorageSideNavKey) === 'true') || false;
    this.sideNavCollapseSource.next(sideNavState);
  }
 
  showNavBar() {
    this.navbarVisibleSource.next(true);
  }

  hideNavBar() {
    this.navbarVisibleSource.next(false);
  }

  showSideNav() {
    this.sideNavCollapseSource.next(true);
  }

  hideSideNav() {
    this.sideNavCollapseSource.next(false);
  }

  toggleSideNav() {
    this.sideNavCollapseSource.pipe(take(1)).subscribe(val => {
      if (val === undefined) val = false;
      const newVal = !(val || false);
      this.sideNavCollapseSource.next(newVal);
      localStorage.setItem(this.localStorageSideNavKey, newVal + '');
    });
  }
  
  toggleSideNavVisibility(forcedState: boolean) {
    this.sideNavVisibility = forcedState;
    this.sideNavVisibilitySource.next(this.sideNavVisibility);
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
