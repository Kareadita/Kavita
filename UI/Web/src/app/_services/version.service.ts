import {inject, Injectable, OnDestroy} from '@angular/core';
import {interval, Subscription, switchMap} from 'rxjs';
import {ServerService} from "./server.service";
import {AccountService} from "./account.service";
import {filter, tap} from "rxjs/operators";
import {NgbModal} from "@ng-bootstrap/ng-bootstrap";
import {NewUpdateModalComponent} from "../announcements/_components/new-update-modal/new-update-modal.component";
import {OutOfDateModalComponent} from "../announcements/_components/out-of-date-modal/out-of-date-modal.component";

@Injectable({
  providedIn: 'root'
})
export class VersionService implements OnDestroy{

  private readonly serverService = inject(ServerService);
  private readonly accountService = inject(AccountService);
  private readonly modalService = inject(NgbModal);

  public static readonly versionKey = 'kavita--version';
  private readonly checkInterval = 600000; // 10 minutes (600000)
  private periodicCheckSubscription?: Subscription;
  private outOfDateCheckSubscription?: Subscription;
  private modalOpen = false;

  constructor() {
    this.startPeriodicUpdateCheck();
    this.startOutOfDateCheck();
  }

  ngOnDestroy() {
    this.periodicCheckSubscription?.unsubscribe();
    this.outOfDateCheckSubscription?.unsubscribe();
  }

  private startOutOfDateCheck() {
    // Every hour, have the UI check for an update. People seriously stay out of date
    this.outOfDateCheckSubscription = interval(2* 60 * 60 * 1000) // 2 hours in milliseconds
      .pipe(
        switchMap(() => this.accountService.currentUser$),
        filter(u => u !== undefined && this.accountService.hasAdminRole(u)),
        switchMap(_ => this.serverService.checkHowOutOfDate()),
        filter(versionOutOfDate => {
          return !isNaN(versionOutOfDate) && versionOutOfDate > 2;
        }),
        tap(versionOutOfDate => {
          if (!this.modalService.hasOpenModals()) {
            const ref = this.modalService.open(OutOfDateModalComponent, {size: 'xl', fullscreen: 'md'});
            ref.componentInstance.versionsOutOfDate = versionOutOfDate;
          }
        })
      )
      .subscribe();
  }

  private startPeriodicUpdateCheck(): void {
    console.log('Starting periodic version update checker');
    this.periodicCheckSubscription = interval(this.checkInterval)
      .pipe(
        switchMap(_ => this.accountService.currentUser$),
        filter(user => user !== undefined && !this.modalOpen),
        switchMap(user => this.serverService.getVersion(user!.apiKey)),
      ).subscribe(version => this.handleVersionUpdate(version));
  }

  private handleVersionUpdate(version: string) {
    if (this.modalOpen) return;

    // Pause periodic checks while the modal is open
    this.periodicCheckSubscription?.unsubscribe();

    const cachedVersion = localStorage.getItem(VersionService.versionKey);
    console.log('Kavita version: ', version, ' Running version: ', cachedVersion);

    const hasChanged = cachedVersion == null || cachedVersion != version;
    if (hasChanged) {
      this.modalOpen = true;

      this.serverService.getChangelog(1).subscribe(changelog => {
        const ref = this.modalService.open(NewUpdateModalComponent, {size: 'lg', keyboard: false});
        ref.componentInstance.version = version;
        ref.componentInstance.update = changelog[0];

        ref.closed.subscribe(_ => this.onModalClosed());
        ref.dismissed.subscribe(_ => this.onModalClosed());

      });

    }

    localStorage.setItem(VersionService.versionKey, version);
  }

  private onModalClosed() {
    this.modalOpen = false;
    this.startPeriodicUpdateCheck();
  }
}
