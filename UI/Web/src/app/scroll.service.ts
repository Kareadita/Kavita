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

  scrollTo(top: number) {
    window.scroll({
      top: top,
      behavior: 'smooth' 
    });
  }  
}
