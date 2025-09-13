import {DOCUMENT} from '@angular/common';
import { HttpClient } from '@angular/common/http';
import {
  DestroyRef,
  inject,
  Inject,
  Injectable,
  Renderer2,
  RendererFactory2,
  SecurityContext
} from '@angular/core';
import {DomSanitizer} from '@angular/platform-browser';
import {ToastrService} from 'ngx-toastr';
import {filter, map, ReplaySubject, take, tap} from 'rxjs';
import {environment} from 'src/environments/environment';
import {ConfirmService} from '../shared/confirm.service';
import {NotificationProgressEvent} from '../_models/events/notification-progress-event';
import {SiteTheme, ThemeProvider} from '../_models/preferences/site-theme';
import {TextResonse} from '../_types/text-response';
import {EVENTS, MessageHubService} from './message-hub.service';
import {takeUntilDestroyed} from "@angular/core/rxjs-interop";
import {translate} from "@jsverse/transloco";
import {DownloadableSiteTheme} from "../_models/theme/downloadable-site-theme";
import {NgxFileDropEntry} from "ngx-file-drop";
import {SiteThemeUpdatedEvent} from "../_models/events/site-theme-updated-event";
import {NavigationEnd, Router} from "@angular/router";
import {ColorscapeService} from "./colorscape.service";
import {ColorScape} from "../_models/theme/colorscape";
import {debounceTime} from "rxjs/operators";

@Injectable({
  providedIn: 'root'
})
export class ThemeService {

  private readonly destroyRef = inject(DestroyRef);
  private readonly colorTransitionService = inject(ColorscapeService);

  public defaultTheme: string = 'dark';
  public defaultBookTheme: string = 'Dark';

  private currentThemeSource = new ReplaySubject<SiteTheme>(1);
  public currentTheme$ = this.currentThemeSource.asObservable();

  private themesSource = new ReplaySubject<SiteTheme[]>(1);
  public themes$ = this.themesSource.asObservable();
  
  private darkModeSource = new ReplaySubject<boolean>(1);
  public isDarkMode$ = this.darkModeSource.asObservable();

  /**
   * Maintain a cache of themes. SignalR will inform us if we need to refresh cache
   */
  private themeCache: Array<SiteTheme> = [];

  private renderer: Renderer2;
  private baseUrl = environment.apiUrl;


  constructor(rendererFactory: RendererFactory2, @Inject(DOCUMENT) private document: Document, private httpClient: HttpClient,
  messageHub: MessageHubService, private domSanitizer: DomSanitizer, private confirmService: ConfirmService, private toastr: ToastrService,
  private router: Router) {
    this.renderer = rendererFactory.createRenderer(null, null);

    messageHub.messages$.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(message => {

      if (message.event === EVENTS.NotificationProgress) {
        const notificationEvent = (message.payload as NotificationProgressEvent);
        if (notificationEvent.name !== EVENTS.SiteThemeProgress) return;

        if (notificationEvent.eventType === 'ended') {
          if (notificationEvent.name === EVENTS.SiteThemeProgress) this.getThemes().subscribe();
        }
        return;
      }

      if (message.event === EVENTS.SiteThemeUpdated) {
        const evt = (message.payload as SiteThemeUpdatedEvent);
        this.currentTheme$.pipe(take(1)).subscribe(currentTheme => {
          if (currentTheme && currentTheme.name !== EVENTS.SiteThemeProgress) return;
          console.log('Active theme has been updated, refreshing theme');
          this.setTheme(currentTheme.name);

        });
      }


    });
  }

  getDownloadableThemes() {
    return this.httpClient.get<Array<DownloadableSiteTheme>>(this.baseUrl + 'theme/browse');
  }

  downloadTheme(theme: DownloadableSiteTheme) {
    return this.httpClient.post<SiteTheme>(this.baseUrl + 'theme/download-theme', theme);
  }

  uploadTheme(themeFile: File, fileEntry: NgxFileDropEntry) {
    const formData = new FormData()
    formData.append('formFile', themeFile, fileEntry.relativePath);

    return this.httpClient.post<SiteTheme>(this.baseUrl + 'theme/upload-theme', formData);
  }

  getColorScheme() {
    return getComputedStyle(this.document.body).getPropertyValue('--color-scheme').trim();
  }

  /**
   * --theme-color from theme. Updates the meta tag
   * @returns
   */
  getThemeColor() {
    return getComputedStyle(this.document.body).getPropertyValue('--theme-color').trim();
  }

  /**
   * --msapplication-TileColor from theme. Updates the meta tag
   * @returns
   */
  getTileColor() {
    return getComputedStyle(this.document.body).getPropertyValue('--title-color').trim();
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
        if (themes.filter(t => t.id === theme.id).length === 0) {
          this.setTheme(this.defaultTheme);
          this.toastr.info(translate('toasts.theme-missing'));
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

  deleteTheme(themeId: number) {
    return this.httpClient.delete(this.baseUrl + 'theme?themeId=' + themeId).pipe(map(() => {
      this.getThemes().subscribe(() => {});
    }));
  }

  setDefault(themeId: number) {
    return this.httpClient.post(this.baseUrl + 'theme/update-default', {themeId: themeId}).pipe(map(() => {
      // Refresh the cache when a default state is changed
      this.getThemes().subscribe(() => {});
    }));
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
   * Set's the background color from a single primary color.
   * @param primaryColor
   * @param complementaryColor
   */
  setColorScape(primaryColor: string, complementaryColor: string | null = null) {
    this.colorTransitionService.setColorScape(primaryColor, complementaryColor);
  }

  /**
   * Trigger a request to get the colors for a given entity and apply them
   * @param entity
   * @param id
   */
  refreshColorScape(entity: 'series' | 'volume' | 'chapter' | 'person', id: number) {
    return this.httpClient.get<ColorScape>(`${this.baseUrl}colorscape/${entity}?id=${id}`).pipe(tap((cs) => {
      this.setColorScape(cs.primary || '', cs.secondary);
    }));
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

      if (theme.provider !== ThemeProvider.System && !this.hasThemeInHead(theme.name)) {
        // We need to load the styles into the browser
        this.fetchThemeContent(theme.id).subscribe(async (content) => {
          if (content === null) {
            await this.confirmService.alert(translate('toasts.alert-bad-theme'));
            this.setTheme('dark');
            return;
          }
          const styleElem = this.document.createElement('style');
          styleElem.id = 'theme-' + theme.name;
          styleElem.appendChild(this.document.createTextNode(content));
          this.renderer.appendChild(this.document.head, styleElem);

          // Check if the theme has --theme-color and apply it to meta tag
          const themeColor = this.getThemeColor();
          if (themeColor) {
            this.document.querySelector('meta[name="theme-color"]')?.setAttribute('content', themeColor);
            this.document.querySelector('meta[name="apple-mobile-web-app-status-bar-style"]')?.setAttribute('content', themeColor);
          }

          const tileColor = this.getTileColor();
          if (tileColor) {
            this.document.querySelector('meta[name="msapplication-TileColor"]')?.setAttribute('content', themeColor);
          }

          const colorScheme = this.getColorScheme();
          if (colorScheme) {
            this.document.querySelector('body')?.setAttribute('theme', colorScheme);
          }

          this.currentThemeSource.next(theme);
          this.darkModeSource.next(this.isDarkTheme());
        });
      } else {
        this.currentThemeSource.next(theme);
        this.darkModeSource.next(this.isDarkTheme());
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
    return this.httpClient.get<string>(this.baseUrl + 'theme/download-content?themeId=' + themeId, TextResonse).pipe(map(encodedCss => {
      return this.domSanitizer.sanitize(SecurityContext.STYLE, encodedCss);
    }));
  }

  private unsetThemes() {
    this.themeCache.forEach(theme => this.document.body.classList.remove(theme.selector));
  }

  private unsetBookThemes() {
    Array.from(this.document.body.classList).filter(cls => cls.startsWith('brtheme-')).forEach(c => this.document.body.classList.remove(c));
  }
}
