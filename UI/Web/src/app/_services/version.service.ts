import {inject, Injectable, OnDestroy} from '@angular/core';
import {interval, Subscription, switchMap} from 'rxjs';
import {ServerService} from "./server.service";
import {AccountService} from "./account.service";
import {filter, take, tap} from "rxjs/operators";
import {Router} from "@angular/router";
import {OpdsName} from "../_models/user/auth-key";
import {
  VersionUpdateModalComponent
} from "../announcements/_components/version-update-modal/version-update-modal.component";
import {versionNotifyModal, versionRefreshModal} from "../_models/modal/modal-options";
import {UpdateVersionEvent} from "../_models/events/update-version-event";
import {ModalService} from "./modal.service";

@Injectable({
  providedIn: 'root'
})
export class VersionService implements OnDestroy{

  private readonly serverService = inject(ServerService);
  private readonly accountService = inject(AccountService);
  private readonly modalService = inject(ModalService);
  private readonly router = inject(Router);

  public static readonly SERVER_VERSION_KEY = 'kavita--version';
  public static readonly CLIENT_REFRESH_KEY = 'kavita--client-refresh-last-shown';
  public static readonly NEW_UPDATE_KEY = 'kavita--new-update-last-shown';
  public static readonly OUT_OF_BAND_KEY = 'kavita--out-of-band-last-shown';

  // Notification intervals
  private readonly CLIENT_REFRESH_INTERVAL = 0; // Show immediately (once)
  private readonly NEW_UPDATE_INTERVAL = 7 * 24 * 60 * 60 * 1000; // 1 week in milliseconds
  private readonly OUT_OF_BAND_INTERVAL = 30 * 24 * 60 * 60 * 1000; // 1 month in milliseconds

  // Check intervals
  private readonly VERSION_CHECK_INTERVAL = 30 * 60 * 1000; // 30 minutes
  private readonly OUT_OF_DATE_CHECK_INTERVAL = 2 * 60 * 60 * 1000; // 2 hours
  private readonly OUT_Of_BAND_AMOUNT = 3; // How many releases before we show "You're X releases out of date"

  // Routes where version update modals should not be shown
  private readonly EXCLUDED_ROUTES = [
    '/manga/',
    '/book/',
    '/pdf/',
    '/reader/'
  ];


  private versionCheckSubscription?: Subscription;
  private outOfDateCheckSubscription?: Subscription;
  private modalOpen = false;
  /** Version fetched on initial page load — used to detect mid-session server updates */
  private loadedVersion: string | null = null;

  constructor() {
    this.startInitialVersionCheck();
    this.startVersionCheck();
    this.startOutOfDateCheck();
  }

  ngOnDestroy() {
    this.versionCheckSubscription?.unsubscribe();
    this.outOfDateCheckSubscription?.unsubscribe();
  }


  /**
   * Initial version check to ensure localStorage is populated on first load
   */
  private startInitialVersionCheck(): void {
    this.accountService.currentUser$
      .pipe(
        filter(user => !!user),
        take(1),
        switchMap(user => this.serverService.getVersion(user!.authKeys.filter(k => k.name === OpdsName)[0].key))
      )
      .subscribe(serverVersion => {
        this.loadedVersion = serverVersion;
        localStorage.setItem(VersionService.SERVER_VERSION_KEY, serverVersion);
        console.log('Initial version check - Server version:', serverVersion);
      });
  }


  /**
   * Periodic check for server version to detect client refreshes and new updates
   */
  private startVersionCheck(): void {
    this.versionCheckSubscription = interval(this.VERSION_CHECK_INTERVAL)
      .pipe(
        switchMap(() => this.accountService.currentUser$),
        filter(user => !!user && !this.modalOpen),
        switchMap(user => this.serverService.getVersion(user!.authKeys.filter(k => k.name === OpdsName)[0].key)),
        filter(update => !!update),
        tap(serverVersion => this.handleVersionCheck(serverVersion))
      ).subscribe();
  }

  /**
   * Checks if the server is out of date compared to the latest release
   */
  private startOutOfDateCheck() {
    this.outOfDateCheckSubscription = interval(this.OUT_OF_DATE_CHECK_INTERVAL)
      .pipe(
        switchMap(() => this.accountService.currentUser$),
        filter(u => u !== undefined && this.accountService.hasAdminRole(u) && !this.modalOpen),
        switchMap(_ => this.serverService.checkHowOutOfDate(true)),
        filter(versionsOutOfDate => !isNaN(versionsOutOfDate) && versionsOutOfDate > this.OUT_Of_BAND_AMOUNT),
        tap(versionsOutOfDate => this.handleOutOfDate(versionsOutOfDate))
      ).subscribe();
  }

  /**
   * Checks if the current route is in the excluded routes list
   */
  isExcludedRoute(): boolean {
    const currentUrl = this.router.url;
    return this.EXCLUDED_ROUTES.some(route => currentUrl.includes(route));
  }

  /**
   * Given a server version string, determines whether to show a refresh modal
   * (server updated mid-session) or check for available updates.
   *
   * Call with the result of `plugin/version`.
   */
  handleVersionCheck(serverVersion: string): void {
    if (this.modalOpen || this.isExcludedRoute()) return;

    const isNewServerVersion = this.loadedVersion !== null && this.loadedVersion !== serverVersion;

    if (isNewServerVersion) {
      // Server was updated mid-session — don't update loadedVersion so the
      // refresh prompt persists until the user actually refreshes.
      localStorage.setItem(VersionService.SERVER_VERSION_KEY, serverVersion);
      this.serverService.getChangelog(1).subscribe(changelog => {
        this.showRefreshModal(changelog[0]);
        localStorage.setItem(VersionService.CLIENT_REFRESH_KEY, Date.now().toString());
      });
    } else {
      this.handleUpdateCheck();
    }
  }

  /**
   * Checks if the admin should be notified of a new update (1–3 versions behind).
   * Fetches versionsOutOfDate from the API, applies weekly throttle, then shows modal.
   */
  handleUpdateCheck(): void {
    this.accountService.currentUser$
      .pipe(
        take(1),
        filter(user => user !== undefined && this.accountService.hasAdminRole(user)),
        switchMap(_ => this.serverService.checkHowOutOfDate()),
        filter(versionsOutOfDate => !isNaN(versionsOutOfDate) && versionsOutOfDate > 0 && versionsOutOfDate <= this.OUT_Of_BAND_AMOUNT),
        tap(versionsOutOfDate => this.handleUpdateAvailable(versionsOutOfDate))
      ).subscribe();
  }

  /**
   * Given a versionsOutOfDate count (1–3), applies weekly throttle and shows the
   * update-available modal if appropriate.
   */
  handleUpdateAvailable(versionsOutOfDate: number): void {
    const lastShown = Number(localStorage.getItem(VersionService.NEW_UPDATE_KEY) || '0');
    const currentTime = Date.now();

    if (currentTime - lastShown < this.NEW_UPDATE_INTERVAL) return;

    this.serverService.getChangelog(1).subscribe(changelog => {
      this.showUpdateAvailableModal(changelog[0], versionsOutOfDate);
      localStorage.setItem(VersionService.NEW_UPDATE_KEY, currentTime.toString());
    });
  }

  /**
   * Given a versionsOutOfDate count (4+), applies monthly throttle and shows the
   * out-of-date modal if appropriate.
   */
  handleOutOfDate(versionsOutOfDate: number): void {
    const lastShown = Number(localStorage.getItem(VersionService.OUT_OF_BAND_KEY) || '0');
    const currentTime = Date.now();

    if (currentTime - lastShown < this.OUT_OF_BAND_INTERVAL) return;

    this.showOutOfDateModal(versionsOutOfDate);
    localStorage.setItem(VersionService.OUT_OF_BAND_KEY, currentTime.toString());
  }

  // endregion

  /**
   * Single entry point for opening version update modals.
   * Prevents stacking — only one modal can be open at a time.
   * Used internally by background checks and externally by EventsWidget / admin settings.
   */
  showUpdateModal(mode: 'refresh' | 'update-available' | 'out-of-date', data: { update?: UpdateVersionEvent | null, versionsOutOfDate?: number } = {}): void {
    if (this.modalOpen) return;

    this.pauseChecks();
    this.modalOpen = true;

    const options = mode === 'refresh' ? versionRefreshModal() : versionNotifyModal();
    const ref = this.modalService.open(VersionUpdateModalComponent, options);
    ref.setInput('mode', mode);

    if (data?.update != null) ref.setInput('update', data.update);
    if (data?.versionsOutOfDate != null) ref.setInput('versionsOutOfDate', data.versionsOutOfDate);

    ref.closed.subscribe(_ => this.onModalClosed());
    ref.dismissed.subscribe(_ => this.onModalClosed());
  }

  /**
   * Shows the refresh-required modal. The server was updated mid-session
   * and the browser needs to reload to pick up new client assets.
   */
  showRefreshModal(update: UpdateVersionEvent): void {
    this.showUpdateModal('refresh', { update });
  }

  /**
   * Shows the update-available modal. A newer version exists that the admin can download.
   */
  showUpdateAvailableModal(update: UpdateVersionEvent, versionsOutOfDate: number = 1): void {
    this.showUpdateModal('update-available', { update, versionsOutOfDate });
  }

  /**
   * Shows the out-of-date warning modal. The server is significantly behind the latest release.
   */
  showOutOfDateModal(versionsOutOfDate: number): void {
    this.showUpdateModal('out-of-date', { versionsOutOfDate });
  }

  debugRefresh(): void {
    this.serverService.getChangelog(1).subscribe(changelog => this.showRefreshModal(changelog[0]));
  }

  debugUpdateAvailable(): void {
    this.serverService.getChangelog(1).subscribe(changelog => this.showUpdateAvailableModal(changelog[0], 2));
  }

  debugOutOfDate(): void {
    this.showOutOfDateModal(5);
  }

  /**
   * Pauses all version checks while modals are open
   */
  private pauseChecks(): void {
    this.versionCheckSubscription?.unsubscribe();
    this.outOfDateCheckSubscription?.unsubscribe();
  }

  /**
   * Resumes all checks when modals are closed
   */
  private onModalClosed(): void {
    this.modalOpen = false;
    this.startVersionCheck();
    this.startOutOfDateCheck();
  }
}
