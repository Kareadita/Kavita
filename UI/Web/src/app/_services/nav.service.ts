import {DOCUMENT} from '@angular/common';
import {DestroyRef, inject, Injectable, Renderer2, RendererFactory2, RendererStyleFlags2, signal} from '@angular/core';
import {filter, take} from 'rxjs';
import {HttpClient} from "@angular/common/http";
import {environment} from "../../environments/environment";
import {SideNavStream} from "../_models/sidenav/sidenav-stream";
import {TextResonse} from "../_types/text-response";
import {AccountService} from "./account.service";
import {map} from "rxjs/operators";
import {NavigationEnd, Router} from "@angular/router";
import {takeUntilDestroyed, toObservable} from "@angular/core/rxjs-interop";
import {WikiLink} from "../_models/wiki";
import {AuthGuard} from "../_guards/auth.guard";

/**
 * NavItem used to construct the dropdown or NavLinkModal on mobile
 * Priority construction
 * @param routerLink A link to a page on the web app, takes priority
 * @param fragment Optional fragment for routerLink
 * @param href A link to an external page, must set noopener noreferrer
 * @param click Callback, lowest priority. Should only be used if routerLink and href or not set
 */
interface NavItem {
  transLocoKey: string;
  href?: string;
  fragment?: string;
  routerLink?: string;
  click?: () => void;
}

@Injectable({
  providedIn: 'root'
})
export class NavService {
  private readonly document = inject<Document>(DOCUMENT);
  private readonly httpClient = inject(HttpClient);
  private readonly accountService = inject(AccountService);
  private readonly router = inject(Router);
  private readonly destroyRef = inject(DestroyRef);

  private readonly renderer: Renderer2;
  private readonly baseUrl = environment.apiUrl;
  public localStorageSideNavKey = 'kavita--sidenav--expanded';

  public navItems: NavItem[] = [
    {
      transLocoKey: 'all-filters',
      routerLink: '/all-filters/',
    },
    {
      transLocoKey: 'browse-genres',
      routerLink: '/browse/genres',
    },
    {
      transLocoKey: 'browse-tags',
      routerLink: '/browse/tags',
    },
    {
      transLocoKey: 'all-annotations',
      routerLink: '/browse/annotations'
    },
    {
      transLocoKey: 'announcements',
      routerLink: '/announcements/',
    },
    {
      transLocoKey: 'help',
      href: WikiLink.Guides,
    },
    {
      transLocoKey: 'logout',
      click: () => this.logout(),
    }
  ]

  private navBarVisible = signal(false);
  /**
   * If the top Nav bar is rendered or not
   */
  navbarVisibleSignal = this.navBarVisible.asReadonly();
  /**
   * If the top Nav bar is rendered or not
   */
  navbarVisible$ = toObservable(this.navBarVisible);


  private sideNavCollapsed = signal(false);
  /**
   * If the Side Nav is in a collapsed state or not.
   */
  sideNavCollapsedSignal = this.sideNavCollapsed.asReadonly();
  /**
   * If the Side Nav is in a collapsed state or not.
   */
  sideNavCollapsed$ = toObservable(this.sideNavCollapsed);

  private sideNavVisibility = signal(false);
  /**
   * If the side nav is rendered or not into the DOM.
   */
  sideNavVisibilitySignal = this.sideNavVisibility.asReadonly();
  /**
   * If the side nav is rendered or not into the DOM.
   */
  sideNavVisibility$ = toObservable(this.sideNavVisibility);

  usePreferenceSideNav$ = this.router.events.pipe(
    filter(event => event instanceof NavigationEnd),
    map((evt) => {
      const event = (evt as NavigationEnd);
      const url = event.urlAfterRedirects || event.url;
      return (
        /\/admin\/dashboard(#.*)?/.test(url) || /\/preferences(\/[^\/]+|#.*)?/.test(url) || /\/settings(\/[^\/]+|#.*)?/.test(url)
      );
    }),
    takeUntilDestroyed(this.destroyRef),
  );



  constructor() {
    const rendererFactory = inject(RendererFactory2);

    this.renderer = rendererFactory.createRenderer(null, null);

    // To avoid flashing, let's check if we are authenticated before we show
    this.accountService.currentUser$.pipe(take(1)).subscribe(u => {
      if (u) {
        this.showNavBar();
      }
    });

    const sideNavState = (localStorage.getItem(this.localStorageSideNavKey) === 'true') || false;
    this.sideNavCollapsed.set(sideNavState);
    this.showSideNav();
  }

  getSideNavStreams(visibleOnly = true) {
    return this.httpClient.get<Array<SideNavStream>>(this.baseUrl + 'stream/sidenav?visibleOnly=' + visibleOnly);
  }

  updateSideNavStreamPosition(streamName: string, sideNavStreamId: number, fromPosition: number, toPosition: number, positionIncludesInvisible: boolean = true) {
    return this.httpClient.post(this.baseUrl + 'stream/update-sidenav-position', {streamName, id: sideNavStreamId, fromPosition, toPosition, positionIncludesInvisible}, TextResonse);
  }

  updateSideNavStream(stream: SideNavStream) {
    return this.httpClient.post(this.baseUrl + 'stream/update-sidenav-stream', stream, TextResonse);
  }

  createSideNavStream(smartFilterId: number) {
    return this.httpClient.post<SideNavStream>(this.baseUrl + 'stream/add-sidenav-stream?smartFilterId=' + smartFilterId, {});
  }

  createSideNavStreamFromExternalSource(externalSourceId: number) {
    return this.httpClient.post<SideNavStream>(this.baseUrl + 'stream/add-sidenav-stream-from-external-source?externalSourceId=' + externalSourceId, {});
  }

  bulkToggleSideNavStreamVisibility(streamIds: Array<number>, targetVisibility: boolean) {
    return this.httpClient.post(this.baseUrl + 'stream/bulk-sidenav-stream-visibility', {ids: streamIds, visibility: targetVisibility});
  }

  deleteSideNavSmartFilter(streamId: number) {
    return this.httpClient.delete(this.baseUrl + 'stream/smart-filter-side-nav-stream?sideNavStreamId=' + streamId, {});
  }

  /**
   * Shows the top nav bar. This should be visible on all pages except the reader.
   */
  showNavBar() {
    setTimeout(() => {
      const bodyElem = this.document.querySelector('body');
      this.renderer.setStyle(bodyElem, 'margin-top', 'var(--nav-offset)');
      this.renderer.removeStyle(bodyElem, 'scrollbar-gutter');
      this.renderer.setStyle(bodyElem, 'height', 'calc(var(--vh)*100 - var(--nav-offset))');
      this.renderer.setStyle(bodyElem, 'overflow', 'hidden');
      this.renderer.setStyle(this.document.querySelector('html'), 'height', 'calc(var(--vh)*100 - var(--nav-offset))');
      this.navBarVisible.set(true);
    }, 10);
  }

  /**
   * Hides the top nav bar.
   */
  hideNavBar() {
    setTimeout(() => {
      const bodyElem = this.document.querySelector('body');
      this.renderer.removeStyle(bodyElem, 'height');
      this.renderer.setStyle(bodyElem, 'margin-top', '0px', RendererStyleFlags2.Important);
      this.renderer.setStyle(bodyElem, 'scrollbar-gutter', 'initial', RendererStyleFlags2.Important);
      this.renderer.removeStyle(this.document.querySelector('html'), 'height');
      this.renderer.setStyle(bodyElem, 'overflow', 'auto');
      this.navBarVisible.set(false);
    }, 10);
  }

  logout() {
    this.hideNavBar();
    this.hideSideNav();
    this.accountService.logout();
  }

  handleLogin() {
    this.showNavBar();
    this.showSideNav();

    // Check if user came here from another url, else send to library route
    const pageResume = localStorage.getItem(AuthGuard.urlKey);
    if (pageResume && pageResume !== '/login') {
      localStorage.setItem(AuthGuard.urlKey, '');
      this.router.navigateByUrl(pageResume);
    } else {
      localStorage.setItem(AuthGuard.urlKey, '');
      this.router.navigateByUrl('/home');
    }
  }

  /**
   * Shows the side nav. When being visible, the side nav will automatically return to previous collapsed state.
   */
  showSideNav() {
    this.sideNavVisibility.set(true);
  }

  /**
   * Hides the side nav. This is useful for the readers and login page.
   */
  hideSideNav() {
    this.sideNavVisibility.set(false);
  }

  toggleSideNav() {
    const newValue = !this.sideNavCollapsed();
    this.sideNavCollapsed.set(newValue);
    localStorage.setItem(this.localStorageSideNavKey, newValue + '');
  }

  collapseSideNav(isCollapsed: boolean) {
    this.sideNavCollapsed.set(isCollapsed);
    localStorage.setItem(this.localStorageSideNavKey, isCollapsed + '');
  }
}
