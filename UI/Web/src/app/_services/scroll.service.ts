import { Injectable } from '@angular/core';

@Injectable({
  providedIn: 'root'
})
export class ScrollService {

  constructor() { }

  get scrollPosition() {
    return (window.pageYOffset 
      || document.documentElement.scrollTop 
      || document.body.scrollTop || 0);
  }

  scrollTo(top: number, el: Element | Window = window) {
    el.scroll({
      top: top,
      behavior: 'smooth' 
    });
  }
  
  scrollToX(left: number, el: Element | Window = window) {
    el.scroll({
      left: left,
      behavior: 'auto'
    });
  }
}
