import { ElementRef, Injectable } from '@angular/core';
import { NavigationEnd, Router } from '@angular/router';
import { filter, ReplaySubject } from 'rxjs';

@Injectable({
  providedIn: 'root'
})
export class ScrollService {

  private scrollContainerSource =  new ReplaySubject<string | ElementRef<HTMLElement>>(1);
  /**
   * Exposes the current container on the active screen that is our primary overlay area. Defaults to 'body' and changes to 'body' on page loads
   */
  public scrollContainer$ = this.scrollContainerSource.asObservable();

  constructor(router: Router) {

    router.events
      .pipe(filter(event => event instanceof NavigationEnd))
      .subscribe(() => {
        this.scrollContainerSource.next('body');
      });
    this.scrollContainerSource.next('body');
  }

  get scrollPosition() {
    return (window.pageYOffset
      || document.documentElement.scrollTop
      || document.body.scrollTop || 0);
  }

  /*
   * When in the scroll vertical position the scroll in the horizontal position is needed
   */
  get scrollPositionX() {
    return (window.pageXOffset
      || document.documentElement.scrollLeft
      || document.body.scrollLeft || 0);
  }

  scrollTo(top: number, el: Element | Window = window, behavior: 'auto' | 'smooth' = 'smooth') {
    el.scroll({
      top: top,
      behavior: behavior
    });
  }

  scrollToX(left: number, el: Element | Window = window, behavior: 'auto' | 'smooth' = 'auto') {
    el.scroll({
      left: left,
      behavior: behavior
    });
  }

  setScrollContainer(elem: ElementRef<HTMLElement> | undefined) {
    if (elem !== undefined) {
      this.scrollContainerSource.next(elem);
    }
  }
}
