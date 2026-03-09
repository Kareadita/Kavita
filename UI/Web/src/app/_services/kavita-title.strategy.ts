import {inject, Injectable} from '@angular/core';
import {Title} from '@angular/platform-browser';
import {RouterStateSnapshot, TitleStrategy} from '@angular/router';
import {TranslocoService} from '@jsverse/transloco';

@Injectable({providedIn: 'root'})
export class KavitaTitleStrategy extends TitleStrategy {
  private readonly title = inject(Title);
  private readonly translocoService = inject(TranslocoService);

  override updateTitle(routerState: RouterStateSnapshot): void {
    // 1. Check for static route title (translation key)
    const routeTitle = this.buildTitle(routerState);
    if (routeTitle) {
      this.setFormattedTitle(routeTitle);
      return;
    }

    // 2. Check for entity-based title from resolved data
    const route = this.getDeepestRoute(routerState.root);
    const titleField = route.data['titleField'];
    if (titleField) {
      const titleProp = route.data['titleProp'] || 'name';
      const titleSuffix = route.data['titleSuffix'] || '';
      const entity = this.findInRouteTree(route, titleField);
      if (entity?.[titleProp]) {
        this.title.setTitle(`Kavita: ${entity[titleProp]}${titleSuffix}`);
        return;
      }
    }

    // 3. Fallback
    this.title.setTitle('Kavita');
  }

  setFormattedTitle(pageTitle: string): void {
    if (pageTitle.startsWith('title.')) {
      pageTitle = this.translocoService.translate(pageTitle);
    }
    this.title.setTitle(`Kavita: ${pageTitle}`);
  }

  setTranslatedTitle(key: string, params: Record<string, unknown>): void {
    this.title.setTitle(`Kavita: ${this.translocoService.translate(key, params)}`);
  }

  private getDeepestRoute(route: RouterStateSnapshot['root']): RouterStateSnapshot['root'] {
    while (route.firstChild) {
      route = route.firstChild;
    }
    return route;
  }

  private findInRouteTree(route: RouterStateSnapshot['root'], field: string): any {
    let current: RouterStateSnapshot['root'] | null = route;
    while (current) {
      if (current.data[field]) {
        return current.data[field];
      }
      current = current.parent;
    }
    return null;
  }
}
