import {inject, Injectable, OnDestroy} from '@angular/core';
import {interval, Subscription, switchMap} from 'rxjs';
import {ServerService} from "./server.service";
import {AccountService} from "./account.service";
import {filter, take} from "rxjs/operators";
import {NgbModal} from "@ng-bootstrap/ng-bootstrap";
import {NewUpdateModalComponent} from "../announcements/_components/new-update-modal/new-update-modal.component";
import {OutOfDateModalComponent} from "../announcements/_components/out-of-date-modal/out-of-date-modal.component";
import {Router} from "@angular/router";
import {OpdsName} from "../_models/user/auth-key";

@Injectable({
  providedIn: 'root'
})
export class VersionService implements OnDestroy{

  private readonly serverService = inject(ServerService);
  private readonly accountService = inject(AccountService);
  private readonly modalService = inject(NgbModal);
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
        const cachedVersion = localStorage.getItem(VersionService.SERVER_VERSION_KEY);

        // Always update localStorage on first load
        localStorage.setItem(VersionService.SERVER_VERSION_KEY, serverVersion);

        console.log('Initial version check - Server version:', serverVersion, 'Cached version:', cachedVersion);
      });
  }

  /**
   * Periodic check for server version to detect client refreshes and new updates
   */
  private startVersionCheck(): void {
    console.log('Starting version checker');
    this.versionCheckSubscription = interval(this.VERSION_CHECK_INTERVAL)
      .pipe(
        switchMap(() => this.accountService.currentUser$),
        filter(user => !!user && !this.modalOpen),
        switchMap(user => this.serverService.getVersion(user!.authKeys.filter(k => k.name === OpdsName)[0].key)),
        filter(update => !!update),
      ).subscribe(version => this.handleVersionUpdate(version));
  }

  /**
   * Checks if the server is out of date compared to the latest release
   */
  private startOutOfDateCheck() {
    console.log('Starting out-of-date checker');
    this.outOfDateCheckSubscription = interval(this.OUT_OF_DATE_CHECK_INTERVAL)
      .pipe(
        switchMap(() => this.accountService.currentUser$),
        filter(u => u !== undefined && this.accountService.hasAdminRole(u) && !this.modalOpen),
        switchMap(_ => this.serverService.checkHowOutOfDate(true)),
        filter(versionsOutOfDate => !isNaN(versionsOutOfDate) && versionsOutOfDate > this.OUT_Of_BAND_AMOUNT),
      )
      .subscribe(versionsOutOfDate => this.handleOutOfDateNotification(versionsOutOfDate));
  }

  /**
   * Checks if the current route is in the excluded routes list
   */
  private isExcludedRoute(): boolean {
    const currentUrl = this.router.url;
    return this.EXCLUDED_ROUTES.some(route => currentUrl.includes(route));
  }

  /**
   * Handles the version check response to determine if client refresh or new update notification is needed
   */
  private handleVersionUpdate(serverVersion: string) {
    if (this.modalOpen) return;

    // Validate if we are on a reader route and if so, suppress
    if (this.isExcludedRoute()) {
      console.log('Version update blocked due to user reading');
      return;
    }

    const cachedVersion = localStorage.getItem(VersionService.SERVER_VERSION_KEY);
    console.log('Server version:', serverVersion, 'Cached version:', cachedVersion);

    const isNewServerVersion = cachedVersion !== null && cachedVersion !== serverVersion;

    // Case 1: Client Refresh needed (server has updated since last client load)
    if (isNewServerVersion) {
      this.showClientRefreshNotification(serverVersion);
    }
    // Case 2: Check for new updates (for server admin)
    else {
      this.checkForNewUpdates();
    }

    // Always update the cached version
    localStorage.setItem(VersionService.SERVER_VERSION_KEY, serverVersion);
  }

  /**
   * Shows a notification that client refresh is needed due to server update
   */
  private showClientRefreshNotification(newVersion: string): void {
    this.pauseChecks();

    // Client refresh notifications should always show (once)
    this.modalOpen = true;

    this.serverService.getChangelog(1).subscribe(changelog => {
      const ref = this.modalService.open(NewUpdateModalComponent, {
        size: 'lg',
        keyboard: false,
        backdrop: 'static' // Prevent closing by clicking outside
      });

      ref.componentInstance.version = newVersion;
      ref.componentInstance.update = changelog[0];
      ref.componentInstance.requiresRefresh = true;

      // Update the last shown timestamp
      localStorage.setItem(VersionService.CLIENT_REFRESH_KEY, Date.now().toString());

      ref.closed.subscribe(_ => this.onModalClosed());
      ref.dismissed.subscribe(_ => this.onModalClosed());
    });
  }

  /**
   * Checks for new server updates and shows notification if appropriate
   */
  private checkForNewUpdates(): void {
    this.accountService.currentUser$
      .pipe(
        take(1),
        filter(user => user !== undefined && this.accountService.hasAdminRole(user)),
        switchMap(_ => this.serverService.checkHowOutOfDate()),
        filter(versionsOutOfDate => !isNaN(versionsOutOfDate) && versionsOutOfDate > 0 && versionsOutOfDate <= this.OUT_Of_BAND_AMOUNT)
      )
      .subscribe(versionsOutOfDate => {
        const lastShown = Number(localStorage.getItem(VersionService.NEW_UPDATE_KEY) || '0');
        const currentTime = Date.now();

        // Show notification if it hasn't been shown in the last week
        if (currentTime - lastShown >= this.NEW_UPDATE_INTERVAL) {
          this.pauseChecks();
          this.modalOpen = true;

          this.serverService.getChangelog(1).subscribe(changelog => {
            const ref = this.modalService.open(NewUpdateModalComponent, { size: 'lg' });
            ref.componentInstance.versionsOutOfDate = versionsOutOfDate;
            ref.componentInstance.update = changelog[0];
            ref.componentInstance.requiresRefresh = false;

            // Update the last shown timestamp
            localStorage.setItem(VersionService.NEW_UPDATE_KEY, currentTime.toString());

            ref.closed.subscribe(_ => this.onModalClosed());
            ref.dismissed.subscribe(_ => this.onModalClosed());
          });
        }
      });
  }

  /**
   * Handles the notification for servers that are significantly out of date
   */
  private handleOutOfDateNotification(versionsOutOfDate: number): void {
    const lastShown = Number(localStorage.getItem(VersionService.OUT_OF_BAND_KEY) || '0');
    const currentTime = Date.now();

    // Show notification if it hasn't been shown in the last month
    if (currentTime - lastShown >= this.OUT_OF_BAND_INTERVAL) {
      this.pauseChecks();
      this.modalOpen = true;

      const ref = this.modalService.open(OutOfDateModalComponent, { size: 'xl', fullscreen: 'md' });
      ref.componentInstance.versionsOutOfDate = versionsOutOfDate;

      // Update the last shown timestamp
      localStorage.setItem(VersionService.OUT_OF_BAND_KEY, currentTime.toString());

      ref.closed.subscribe(_ => this.onModalClosed());
      ref.dismissed.subscribe(_ => this.onModalClosed());
    }
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
