import { Injectable } from '@angular/core';
import { ReplaySubject, take } from 'rxjs';

@Injectable({
  providedIn: 'root'
})
export class NavService {
  public localStorageSideNavKey = 'kavita--sidenav--collapsed';

  private navbarVisibleSource = new ReplaySubject<boolean>(1);
  /**
   * If the top Nav bar is rendered or not
   */
  navbarVisible$ = this.navbarVisibleSource.asObservable();

  private sideNavCollapseSource = new ReplaySubject<boolean>(1);
  /**
   * If the Side Nav is in a collapsed state or not.
   */
  sideNavCollapsed$ = this.sideNavCollapseSource.asObservable();

  private sideNavVisibilitySource = new ReplaySubject<boolean>(1);
  /**
   * If the side nav is rendered or not into the DOM.
   */
  sideNavVisibility$ = this.sideNavVisibilitySource.asObservable();

  constructor() {
    this.showNavBar();
    const sideNavState = (localStorage.getItem(this.localStorageSideNavKey) === 'true') || false;
    this.sideNavCollapseSource.next(sideNavState);
    this.showSideNav();
  }
 
  /**
   * Shows the top nav bar. This should be visible on all pages except the reader.
   */
  showNavBar() {
    this.navbarVisibleSource.next(true);
  }

  /**
   * Hides the top nav bar. 
   */
  hideNavBar() {
    this.navbarVisibleSource.next(false);
  }

  /**
   * Shows the side nav. When being visible, the side nav will automatically return to previous collapsed state.
   */
  showSideNav() {
    this.sideNavVisibilitySource.next(true);
  }

  /**
   * Hides the side nav. This is useful for the readers and login page.
   */
  hideSideNav() {
    this.sideNavVisibilitySource.next(false);
  }

  toggleSideNav() {
    this.sideNavCollapseSource.pipe(take(1)).subscribe(val => {
      if (val === undefined) val = false;
      const newVal = !(val || false);
      this.sideNavCollapseSource.next(newVal);
      localStorage.setItem(this.localStorageSideNavKey, newVal + '');
    });
  }
  
  /**
   * Completely show/hide the side nav. Useful in situations like when reading.
   * @param forcedState 
   */
  toggleSideNavVisibility(forcedState: boolean) {
    this.sideNavVisibilitySource.next(forcedState);
  }
}
