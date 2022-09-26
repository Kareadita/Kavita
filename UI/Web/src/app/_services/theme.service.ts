import { DOCUMENT } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { Inject, Injectable, OnDestroy, Renderer2, RendererFactory2, SecurityContext } from '@angular/core';
import { DomSanitizer } from '@angular/platform-browser';
import { ToastrService } from 'ngx-toastr';
import { map, ReplaySubject, Subject, takeUntil, take, distinctUntilChanged, Observable } from 'rxjs';
import { environment } from 'src/environments/environment';
import { ConfirmService } from '../shared/confirm.service';
import { NotificationProgressEvent } from '../_models/events/notification-progress-event';
import { SiteTheme, ThemeProvider } from '../_models/preferences/site-theme';
import { AccountService } from './account.service';
import { EVENTS, MessageHubService } from './message-hub.service';


@Injectable({
  providedIn: 'root'
})
export class ThemeService implements OnDestroy {

  public defaultTheme: string = 'dark';
  public defaultBookTheme: string = 'Dark';

  private currentThemeSource = new ReplaySubject<SiteTheme>(1);
  public currentTheme$ = this.currentThemeSource.asObservable();

  private themesSource = new ReplaySubject<SiteTheme[]>(1);
  public themes$ = this.themesSource.asObservable();
  
  /**
   * Maintain a cache of themes. SignalR will inform us if we need to refresh cache
   */
  private themeCache: Array<SiteTheme> = [];

  private readonly onDestroy = new Subject<void>();
  private renderer: Renderer2;
  private baseUrl = environment.apiUrl;


  constructor(rendererFactory: RendererFactory2, @Inject(DOCUMENT) private document: Document, private httpClient: HttpClient,
  messageHub: MessageHubService, private domSantizer: DomSanitizer, private confirmService: ConfirmService, private toastr: ToastrService) {
    this.renderer = rendererFactory.createRenderer(null, null);

    this.getThemes();

    messageHub.messages$.pipe(takeUntil(this.onDestroy)).subscribe(message => {

      if (message.event !== EVENTS.NotificationProgress) return;
      const notificationEvent = (message.payload as NotificationProgressEvent);
      if (notificationEvent.name !== EVENTS.SiteThemeProgress) return;

      if (notificationEvent.eventType === 'ended') {
        if (notificationEvent.name === EVENTS.SiteThemeProgress) this.getThemes().subscribe(() => {

        });
      }
    });
  }

  ngOnDestroy(): void {
    this.onDestroy.next();
    this.onDestroy.complete();
  }

  getColorScheme() {
    return getComputedStyle(this.document.body).getPropertyValue('--color-scheme').trim();
  }

  getCssVariable(variable: string) {
    return getComputedStyle(this.document.body).getPropertyValue(variable).trim();
  }

  isDarkTheme() {
    return this.getColorScheme().toLowerCase() === 'dark';
  }

  getThemes() {
    return this.httpClient.get<SiteTheme[]>(this.baseUrl + 'theme').pipe(map(themes => {
      this.themeCache = themes;
      this.themesSource.next(themes);
      this.currentTheme$.pipe(take(1)).subscribe(theme => {
        if (!themes.includes(theme)) {
          this.setTheme(this.defaultTheme);
          this.toastr.info('The active theme no longer exists. Please refresh the page.');
        }
      });
      return themes;
    }));
  }

  /**
   * Used in book reader to remove all themes so book reader can provide custom theming options
   */
  clearThemes() {
    this.unsetThemes();
  }

  setDefault(themeId: number) {
    return this.httpClient.post(this.baseUrl + 'theme/update-default', {themeId: themeId}).pipe(map(() => {
      // Refresh the cache when a default state is changed
      this.getThemes().subscribe(() => {});
    }));
  }

  scan() {
    return this.httpClient.post(this.baseUrl + 'theme/scan', {});
  }

  /**
   * Sets the book theme on the body tag so css variable overrides can take place
   * @param selector brtheme- prefixed string
   */
  setBookTheme(selector: string) {
    this.unsetBookThemes();
    this.renderer.addClass(this.document.querySelector('body'), selector);
  }

  clearBookTheme() {
    this.unsetBookThemes();
  }


  /**
   * Sets the theme as active. Will inject a style tag into document to load a custom theme and apply the selector to the body
   * @param themeName
   */
   setTheme(themeName: string) {
    const theme = this.themeCache.find(t => t.name.toLowerCase() === themeName.toLowerCase());
    if (theme) {
      this.unsetThemes();
      this.renderer.addClass(this.document.querySelector('body'), theme.selector);

      if (theme.provider === ThemeProvider.User && !this.hasThemeInHead(theme.name)) {
        // We need to load the styles into the browser
        this.fetchThemeContent(theme.id).subscribe(async (content) => {
          if (content === null) {
            await this.confirmService.alert('There is invalid or unsafe css in the theme. Please reach out to your admin to have this corrected. Defaulting to dark theme.');
            this.setTheme('dark');
            return;
          }
          const styleElem = document.createElement('style');
          styleElem.id = 'theme-' + theme.name;
          styleElem.appendChild(this.document.createTextNode(content));

          this.renderer.appendChild(this.document.head, styleElem);
          this.currentThemeSource.next(theme);
        });
      } else {
        this.currentThemeSource.next(theme);
      }
    } else {
      // Only time themes isn't already loaded is on first load
      this.getThemes().subscribe(themes => {
        this.setTheme(themeName);
      });
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

  private unsetBookThemes() {
    Array.from(this.document.body.classList).filter(cls => cls.startsWith('brtheme-')).forEach(c => this.document.body.classList.remove(c));
  }


}
