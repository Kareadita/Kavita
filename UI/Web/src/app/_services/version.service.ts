import {DestroyRef, inject, Injectable, signal} from '@angular/core';
import {interval, Subscription, switchMap} from 'rxjs';
import {ServerService} from "./server.service";
import {AccountService} from "./account.service";
import {filter, map, take, tap} from "rxjs/operators";
import {Router} from "@angular/router";
import {
  VersionUpdateModalComponent
} from "../announcements/_components/version-update-modal/version-update-modal.component";
import {versionNotifyModal, versionRefreshModal} from "../_models/modal/modal-options";
import {UpdateVersionEvent} from "../_models/events/update-version-event";
import {ModalService} from "./modal.service";
import {takeUntilDestroyed, toObservable} from "@angular/core/rxjs-interop";

@Injectable({
  providedIn: 'root'
})
export class VersionService {

  private readonly serverService = inject(ServerService);
  private readonly accountService = inject(AccountService);
  private readonly modalService = inject(ModalService);
  private readonly router = inject(Router);
  private readonly destroyRef = inject(DestroyRef);

  public static readonly SERVER_VERSION_KEY = 'kavita--version';
  public static readonly CLIENT_REFRESH_KEY = 'kavita--client-refresh-last-shown';
  private static readonly DISMISS_KEY_PREFIX = 'kavita--update-dismiss-';

  private readonly _currentVersion = signal<string | undefined>(undefined);
  public readonly currentVersion = this._currentVersion.asReadonly();

  private readonly VERSION_CHECK_INTERVAL = 30 * 60 * 1000; // 30 minutes
  /** Threshold: above this count shows "out of date" instead of "update available" */
  private readonly OUT_OF_BAND_AMOUNT = 3;

  /** Backoff intervals indexed by dismiss count: [after 1st dismiss, after 2nd dismiss] */
  private readonly BACKOFF_INTERVALS = [
    1 * 24 * 60 * 60 * 1000,  // 1 day
    3 * 24 * 60 * 60 * 1000,  // 3 days
    7 * 24 * 60 * 60 * 1000,  // 1 week
    14 * 24 * 60 * 60 * 1000, // 2 weeks
    30 * 24 * 60 * 60 * 1000, // 1 month
  ];
  /** After this many dismissals, Kavita will stop pestering the user */
  private readonly MAX_DISMISSALS = 5;

  /** Routes where version update modals should not be shown */
  private readonly EXCLUDED_ROUTES = [
    '/manga/',
    '/book/',
    '/pdf/',
    '/reader/'
  ];


  private versionCheckSubscription?: Subscription;
  private modalOpen = false;
  /** Version fetched on initial page load - used to detect mid-session server updates */
  private loadedVersion: string | null = null;
  /** Tracks which version the currently-open modal is for, so we can record dismissal on close */
  private activeModalVersion: string | null = null;

  constructor() {
    this.startInitialVersionCheck();
    this.startVersionCheck();
  }

  /**
   * Initial version check to ensure localStorage is populated on first load
   */
  private startInitialVersionCheck(): void {
    toObservable(this.accountService.currentUserGenericApiKey).pipe(
      filter((key): key is string => !!key),
      take(1),
      switchMap(key => this.serverService.getVersion(key))
    ).subscribe(serverVersion => {
      this.loadedVersion = serverVersion;
      localStorage.setItem(VersionService.SERVER_VERSION_KEY, serverVersion);
      this._currentVersion.set(serverVersion);
      this.cleanupOldDismissals(serverVersion);
      console.log('Initial version check - Server version:', serverVersion);
    });
  }


  /**
   * Periodic check for server version to detect client refreshes and new updates
   */
  private startVersionCheck(): void {
    this.versionCheckSubscription = interval(this.VERSION_CHECK_INTERVAL)
      .pipe(
        map(() => this.accountService.currentUserGenericApiKey()),
        filter((key): key is string => !!key && !this.modalOpen),
        switchMap(key => this.serverService.getVersion(key)),
        filter(update => !!update),
        tap(serverVersion => this.handleVersionCheck(serverVersion)),
        takeUntilDestroyed(this.destroyRef)
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
   */
  handleVersionCheck(serverVersion: string): void {
    if (this.modalOpen || this.isExcludedRoute()) return;

    const isNewServerVersion = this.loadedVersion !== null && this.loadedVersion !== serverVersion;

    if (isNewServerVersion) {
      // Server was updated mid-session - don't update loadedVersion so the
      // refresh prompt persists until the user actually refreshes.
      localStorage.setItem(VersionService.SERVER_VERSION_KEY, serverVersion);
      this._currentVersion.set(serverVersion);
      this.serverService.getChangelog(1).subscribe(changelog => {
        this.showRefreshModal(changelog[0]);
        localStorage.setItem(VersionService.CLIENT_REFRESH_KEY, Date.now().toString());
      });
    } else {
      this.handleUpdateCheck();
    }
  }

  /**
   * Checks if the admin should be notified of a new update or that the server is significantly out of date.
   * Single API call to checkHowOutOfDate determines which modal (if any) to show.
   */
  handleUpdateCheck(): void {
    if (!this.accountService.hasAdminRole()) return;
    this.serverService.checkHowOutOfDate().pipe(
      filter(versionsOutOfDate => !isNaN(versionsOutOfDate) && versionsOutOfDate > 0),
    ).subscribe(versionsOutOfDate => {
      if (versionsOutOfDate > this.OUT_OF_BAND_AMOUNT) {
        this.handleOutOfDate(versionsOutOfDate);
      } else {
        this.handleUpdateAvailable(versionsOutOfDate);
      }
    });
  }

  /**
   * Given a versionsOutOfDate count (1–3), fetches changelog and shows the
   * update-available modal. Backoff is applied in showUpdateModal.
   */
  handleUpdateAvailable(versionsOutOfDate: number): void {
    this.serverService.getChangelog(1).subscribe(changelog => {
      this.showUpdateAvailableModal(changelog[0], versionsOutOfDate);
    });
  }

  /**
   * Given a versionsOutOfDate count (4+), shows the out-of-date modal.
   * Backoff is applied in showUpdateModal.
   */
  handleOutOfDate(versionsOutOfDate: number): void {
    this.showOutOfDateModal(versionsOutOfDate);
  }

  /**
   * Single entry point for opening version update modals.
   * Prevents stacking, only one modal can be open at a time.
   * For non-refresh modes, applies per-version backoff before opening.
   */
  showUpdateModal(mode: 'refresh' | 'update-available' | 'out-of-date', data: { update?: UpdateVersionEvent | null, versionsOutOfDate?: number } = {}, force: boolean = false): void {
    if (this.modalOpen) return;

    // Per-version backoff for dismissible modes (skipped for refresh and user-initiated actions)
    if (mode !== 'refresh' && !force) {
      const backoffVersion = this.getBackoffVersion(mode, data);
      if (backoffVersion && !this.shouldShowNotification(backoffVersion)) return;
      this.activeModalVersion = backoffVersion;
    }

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

  /**
   * Determines the version string used for backoff tracking.
   * update-available: the version available to update to.
   * out-of-date: the current server version (resets when user updates).
   */
  private getBackoffVersion(mode: string, data: { update?: UpdateVersionEvent | null }): string | null {
    if (mode === 'update-available') return data.update?.updateVersion ?? null;
    if (mode === 'out-of-date') return this.loadedVersion;
    return null;
  }

  /**
   * Checks per-version dismissal history to determine if we should show the notification.
   * Returns false if the user has dismissed enough times or too recently.
   */
  shouldShowNotification(targetVersion: string): boolean {
    const raw = localStorage.getItem(VersionService.DISMISS_KEY_PREFIX + targetVersion);
    if (!raw) return true;

    const { count, lastDismissed } = JSON.parse(raw) as { count: number; lastDismissed: number };
    if (count >= this.MAX_DISMISSALS) return false;

    const interval = this.BACKOFF_INTERVALS[Math.min(count - 1, this.BACKOFF_INTERVALS.length - 1)];
    return Date.now() - lastDismissed >= interval;
  }

  /**
   * Records a dismissal for the given version, incrementing the count and updating the timestamp.
   */
  recordDismissal(targetVersion: string): void {
    const raw = localStorage.getItem(VersionService.DISMISS_KEY_PREFIX + targetVersion);
    const current = raw ? JSON.parse(raw) as { count: number } : { count: 0 };
    localStorage.setItem(VersionService.DISMISS_KEY_PREFIX + targetVersion, JSON.stringify({
      count: current.count + 1,
      lastDismissed: Date.now(),
    }));
  }

  /**
   * Removes dismiss keys for versions other than the current server version.
   * Prevents stale backoff data from carrying over after an update.
   */
  private cleanupOldDismissals(currentVersion: string): void {
    const keepKey = VersionService.DISMISS_KEY_PREFIX + currentVersion;
    for (let i = localStorage.length - 1; i >= 0; i--) {
      const key = localStorage.key(i);
      if (key && key.startsWith(VersionService.DISMISS_KEY_PREFIX) && key !== keepKey) {
        localStorage.removeItem(key);
      }
    }
  }

  private pauseChecks(): void {
    this.versionCheckSubscription?.unsubscribe();
  }

  private onModalClosed(): void {
    if (this.activeModalVersion) {
      this.recordDismissal(this.activeModalVersion);
      this.activeModalVersion = null;
    }
    this.modalOpen = false;
    this.startVersionCheck();
  }
}
