import { Injectable, Renderer2, RendererFactory2 } from '@angular/core';
import { ReplaySubject } from 'rxjs';

@Injectable({
  providedIn: 'root'
})
export class NavService {

  private navbarVisibleSource = new ReplaySubject<boolean>(1);
  navbarVisible$ = this.navbarVisibleSource.asObservable();

  // private darkMode: boolean = true;
  // private darkModeSource = new ReplaySubject<boolean>(1);
  // darkMode$ = this.darkModeSource.asObservable();

  private renderer: Renderer2;

  constructor(rendererFactory: RendererFactory2) {
    this.renderer = rendererFactory.createRenderer(null, null);
    this.showNavBar();
  }

  showNavBar() {
    this.navbarVisibleSource.next(true);
  }

  hideNavBar() {
    this.navbarVisibleSource.next(false);
  }

  // toggleDarkMode() {
  //   this.darkMode = !this.darkMode;
  //   this.updateColorScheme();
  //   this.darkModeSource.next(this.darkMode);
  // }

  // setDarkMode(mode: boolean) {
  //   this.darkMode = mode;
  //   this.updateColorScheme();
  //   this.darkModeSource.next(this.darkMode);
  // }

  // private updateColorScheme() {
  //   if (this.darkMode) {
  //     this.renderer.setStyle(document.querySelector('html'), 'color-scheme', 'dark');
  //   } else {
  //     this.renderer.setStyle(document.querySelector('html'), 'color-scheme', 'light');
  //   }
  // }


}
