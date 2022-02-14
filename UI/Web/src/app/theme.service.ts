import { DOCUMENT } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { Inject, Injectable, OnDestroy, Renderer2, RendererFactory2, SecurityContext } from '@angular/core';
import { DomSanitizer } from '@angular/platform-browser';
import { map, ReplaySubject, Subject, takeUntil } from 'rxjs';
import { environment } from 'src/environments/environment';
import { SiteTheme, ThemeProvider } from './_models/preferences/site-theme';
import { EVENTS, MessageHubService } from './_services/message-hub.service';



@Injectable({
  providedIn: 'root'
})
export class ThemeService implements OnDestroy {

  public defaultTheme: string = 'dark';

  private currentTheme: SiteTheme | undefined;
  private currentThemeSource = new ReplaySubject<SiteTheme>(1);
  public currentTheme$ = this.currentThemeSource.asObservable();

  /**
   * Maintain a cache of themes. SignalR will inform us if we need to refresh cache
   */
  private themeCache: Array<SiteTheme> = [];

  private readonly onDestroy = new Subject<void>();
  private renderer: Renderer2;
  private baseUrl = environment.apiUrl;

  constructor(rendererFactory: RendererFactory2, @Inject(DOCUMENT) private document: Document, private httpClient: HttpClient, 
  messageHub: MessageHubService, private domSantizer: DomSanitizer) {
    this.renderer = rendererFactory.createRenderer(null, null);

    messageHub.messages$.pipe(takeUntil(this.onDestroy)).subscribe(message => {
      // if (message.event === EVENTS.ThemeUpdate) {
      //   // TODO
      // }
    });
  }

  ngOnDestroy(): void {
    this.onDestroy.next();
    this.onDestroy.complete();
  }

  getThemes() {
    return this.httpClient.get<SiteTheme[]>(this.baseUrl + 'theme').pipe(map(themes => {
      this.themeCache = themes;
      return themes;
    }));
  }

  scan() {
    return this.httpClient.post(this.baseUrl + 'theme/scan', {});
  }


  setTheme(themeName: string) {
    const theme = this.themeCache.find(t => t.name.toLowerCase() === themeName.toLowerCase());
    if (theme) {
      this.unsetThemes();

      if (theme.provider === ThemeProvider.User && !this.hasThemeInHead(theme.name)) {
        // We need to load the styles into the browser
        this.fetchThemeContent(theme.id).subscribe((content) => {
          if (content === null) {
            console.error('There is invalid or unsafe css in the theme!');
            this.setTheme('dark');
            return;
          }
          const styleElem = document.createElement('style');
          styleElem.id = 'theme-' + theme.name;
          styleElem.appendChild(this.document.createTextNode(content));

          this.renderer.appendChild(this.document.head, styleElem);
          this.currentTheme = theme;
        });
      }

      this.renderer.addClass(this.document.querySelector('body'), theme.selector);
      this.currentTheme = theme;
    } else {
      // TODO: Do I need a flow for if theme isn't cached? 
    }
  }

  private hasThemeInHead(themeName: string) {
    const id = 'theme-' + themeName.toLowerCase();
    return Array.from(this.document.head.children).filter(el => el.tagName === 'STYLE' && el.id.toLowerCase() === id).length > 0;
  }

  private fetchThemeContent(themeId: number) {
    // TODO: Refactor {responseType: 'text' as 'json'} into a type so i don't have to retype it
    return this.httpClient.get<string>(this.baseUrl + 'theme/download-content?themeId=' + themeId, {responseType: 'text' as 'json'}).pipe(map(encodedCss => {
      return this.domSantizer.sanitize(SecurityContext.STYLE, encodedCss);
    }));
  }

  private unsetThemes() {
    this.themeCache.forEach(theme => this.document.body.classList.remove(theme.selector));
  }
}
