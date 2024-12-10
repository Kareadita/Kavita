import {DOCUMENT} from '@angular/common';
import {DestroyRef, inject, Inject, Injectable, Renderer2, RendererFactory2, RendererStyleFlags2} from '@angular/core';
import {distinctUntilChanged, filter, ReplaySubject, take} from 'rxjs';
import { HttpClient } from "@angular/common/http";
import {environment} from "../../environments/environment";
import {SideNavStream} from "../_models/sidenav/sidenav-stream";
import {TextResonse} from "../_types/text-response";
import {AccountService} from "./account.service";
import {map} from "rxjs/operators";
import {NavigationEnd, Router} from "@angular/router";
import {takeUntilDestroyed} from "@angular/core/rxjs-interop";

@Injectable({
  providedIn: 'root'
})
export class NavService {

  private readonly accountService = inject(AccountService);
  private readonly router = inject(Router);
  private readonly destroyRef = inject(DestroyRef);

  public localStorageSideNavKey = 'kavita--sidenav--expanded';

  private navbarVisibleSource = new ReplaySubject<boolean>(1);
  /**
   * If the top Nav bar is rendered or not
   */
  navbarVisible$ = this.navbarVisibleSource.asObservable();

  private sideNavCollapseSource = new ReplaySubject<boolean>(1);
  /**
   * If the Side Nav is in a collapsed state or not.
   */
  sideNavCollapsed$ = this.sideNavCollapseSource.asObservable();

  private sideNavVisibilitySource = new ReplaySubject<boolean>(1);
  /**
   * If the side nav is rendered or not into the DOM.
   */
  sideNavVisibility$ = this.sideNavVisibilitySource.asObservable();

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

  private renderer: Renderer2;
  baseUrl = environment.apiUrl;

  constructor(@Inject(DOCUMENT) private document: Document, rendererFactory: RendererFactory2, private httpClient: HttpClient) {
    this.renderer = rendererFactory.createRenderer(null, null);

    // To avoid flashing, let's check if we are authenticated before we show
    this.accountService.currentUser$.pipe(take(1)).subscribe(u => {
      if (u) {
        this.showNavBar();
      }
    });

    const sideNavState = (localStorage.getItem(this.localStorageSideNavKey) === 'true') || false;
    this.sideNavCollapseSource.next(sideNavState);
    this.showSideNav();
  }

  getSideNavStreams(visibleOnly = true) {
    return this.httpClient.get<Array<SideNavStream>>(this.baseUrl + 'stream/sidenav?visibleOnly=' + visibleOnly);
  }

  updateSideNavStreamPosition(streamName: string, sideNavStreamId: number, fromPosition: number, toPosition: number) {
    return this.httpClient.post(this.baseUrl + 'stream/update-sidenav-position', {streamName, id: sideNavStreamId, fromPosition, toPosition}, TextResonse);
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
      this.navbarVisibleSource.next(true);
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
      this.navbarVisibleSource.next(false);
    }, 10);
  }

  /**
   * Shows the side nav. When being visible, the side nav will automatically return to previous collapsed state.
   */
  showSideNav() {
    this.sideNavVisibilitySource.next(true);
  }

  /**
   * Hides the side nav. This is useful for the readers and login page.
   */
  hideSideNav() {
    this.sideNavVisibilitySource.next(false);
  }

  toggleSideNav() {
    this.sideNavCollapseSource.pipe(take(1)).subscribe(val => {
      if (val === undefined) val = false;
      const newVal = !(val || false);
      this.sideNavCollapseSource.next(newVal);
      localStorage.setItem(this.localStorageSideNavKey, newVal + '');
    });
  }

  collapseSideNav(isCollapsed: boolean) {
    this.sideNavCollapseSource.next(isCollapsed);
    localStorage.setItem(this.localStorageSideNavKey, isCollapsed + '');
  }
}
