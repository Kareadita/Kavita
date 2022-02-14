import { style } from '@angular/animations';
import { DOCUMENT } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { Inject, Injectable, Renderer2, RendererFactory2, SecurityContext } from '@angular/core';
import { DomSanitizer } from '@angular/platform-browser';
import { map } from 'rxjs';
import { environment } from 'src/environments/environment';
import { MessageHubService } from './_services/message-hub.service';

/**
 * Where does the theme come from
 */
export enum ThemeProvider {
  System = 1,
  User = 2
}

/**
 * Theme for the whole instance
 */
export interface SiteTheme {
  id: number;
  name: string;
  filePath: string;
  isDefault: boolean;
  type: ThemeProvider;
  /**
   * The actual class the root is defined against. It is generated at the backend.
   */
  selector: string;
}

@Injectable({
  providedIn: 'root'
})
export class ThemeService {

  baseUrl = environment.apiUrl;

  private renderer: Renderer2;

  /**
   * Maintain a cache of themes. SignalR will inform us if we need to refresh cache
   */
  private themeCache: Array<SiteTheme> = [
    {id: 1, filePath: './assets/theme/themes/light.css', isDefault: false, name: 'Light', type: ThemeProvider.System, selector: 'bg-light'},
    {id: 2, filePath: './assets/theme/themes/dark.css', isDefault: false, name: 'Dark', type: ThemeProvider.System, selector: 'bg-dark'},
    {id: 3, filePath: './assets/theme/themes/eink.css', isDefault: false, name: 'E-Ink', type: ThemeProvider.System, selector: 'bg-eink'},
    {id: 3, filePath: 'themes/custom.css', isDefault: false, name: 'Custom', type: ThemeProvider.User, selector: 'bg-custom'},
  ];

  constructor(rendererFactory: RendererFactory2, @Inject(DOCUMENT) private document: Document, private httpClient: HttpClient, private messageHub: MessageHubService, private domSantizer: DomSanitizer) {
    this.renderer = rendererFactory.createRenderer(null, null);
  }

  setTheme(themeName: string) {
    const theme = this.themeCache.find(t => t.name.toLowerCase() === themeName.toLowerCase());
    if (theme) {
      console.log('Switching to theme: ', theme.name);
      this.unsetThemes();

      if (theme.type === ThemeProvider.User && !this.hasThemeInHead(theme.name)) {
        // We need to load the styles into the browser
        this.fetchThemeContent(theme.id).subscribe((content) => {
          if (content === null) {
            console.error('There is invalid or unsafe css in the theme!');
            return;
          }
          const styleElem = document.createElement('style');
          styleElem.id = 'theme-' + theme.name;
          styleElem.appendChild(this.document.createTextNode(content));

          this.renderer.appendChild(this.document.head, styleElem);
        });
      }

      this.renderer.addClass(document.querySelector('body'), theme.selector);
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
