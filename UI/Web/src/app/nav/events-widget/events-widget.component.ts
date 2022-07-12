import { ChangeDetectionStrategy, ChangeDetectorRef, Component, Input, OnDestroy, OnInit } from '@angular/core';
import { NgbModal, NgbModalRef } from '@ng-bootstrap/ng-bootstrap';
import { BehaviorSubject, Observable, of, Subject, tap } from 'rxjs';
import { map, shareReplay, takeUntil } from 'rxjs/operators';
import { ConfirmConfig } from 'src/app/shared/confirm-dialog/_models/confirm-config';
import { ConfirmService } from 'src/app/shared/confirm.service';
import { UpdateNotificationModalComponent } from 'src/app/shared/update-notification/update-notification-modal.component';
import { DownloadService } from 'src/app/shared/_services/download.service';
import { ErrorEvent } from 'src/app/_models/events/error-event';
import { NotificationProgressEvent } from 'src/app/_models/events/notification-progress-event';
import { UpdateVersionEvent } from 'src/app/_models/events/update-version-event';
import { User } from 'src/app/_models/user';
import { AccountService } from 'src/app/_services/account.service';
import { EVENTS, Message, MessageHubService } from 'src/app/_services/message-hub.service';

@Component({
  selector: 'app-nav-events-toggle',
  templateUrl: './events-widget.component.html',
  styleUrls: ['./events-widget.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class EventsWidgetComponent implements OnInit, OnDestroy {
  @Input() user!: User;

  isAdmin$: Observable<boolean> = of(false);

  private readonly onDestroy = new Subject<void>();

  /**
   * Progress events (Event Type: 'started', 'ended', 'updated' that have progress property)
   */
  progressEventsSource = new BehaviorSubject<NotificationProgressEvent[]>([]);
  progressEvents$ = this.progressEventsSource.asObservable();

  singleUpdateSource = new BehaviorSubject<NotificationProgressEvent[]>([]);
  singleUpdates$ = this.singleUpdateSource.asObservable();

  errorSource = new BehaviorSubject<ErrorEvent[]>([]);
  errors$ = this.errorSource.asObservable();

  private updateNotificationModalRef: NgbModalRef | null = null;

  activeEvents: number = 0;

  debugMode: boolean = true;


  get EVENTS() {
    return EVENTS;
  }

  constructor(public messageHub: MessageHubService, private modalService: NgbModal, 
    private accountService: AccountService, private confirmService: ConfirmService,
    private readonly cdRef: ChangeDetectorRef, public downloadService: DownloadService) { }

  ngOnDestroy(): void {
    this.onDestroy.next();
    this.onDestroy.complete();
    this.progressEventsSource.complete();
    this.singleUpdateSource.complete();
    this.errorSource.complete();
  }

  ngOnInit(): void {
    this.messageHub.messages$.pipe(takeUntil(this.onDestroy)).subscribe(event => {
      if (event.event === EVENTS.NotificationProgress) {
        this.processNotificationProgressEvent(event);
      } else if (event.event === EVENTS.Error) {
        const values = this.errorSource.getValue();
        values.push(event.payload as ErrorEvent);
        this.errorSource.next(values);
        this.activeEvents += 1;
        this.cdRef.markForCheck();
      }
    });

    this.isAdmin$ = this.accountService.currentUser$.pipe(
      takeUntil(this.onDestroy), 
      map(user => (user && this.accountService.hasAdminRole(user)) || false), 
      shareReplay()
    );
  }

  processNotificationProgressEvent(event: Message<NotificationProgressEvent>) {
    const message = event.payload as NotificationProgressEvent;
    let data;
    switch (event.payload.eventType) {
      case 'single':
        const values = this.singleUpdateSource.getValue();
        values.push(message);
        this.singleUpdateSource.next(values);
        this.activeEvents += 1;
        this.cdRef.markForCheck();
        break;
      case 'started':
        // Sometimes we can receive 2 started on long running scans, so better to just treat as a merge then.
        data = this.mergeOrUpdate(this.progressEventsSource.getValue(), message);
        this.progressEventsSource.next(data);
        break;
      case 'updated':
        data = this.mergeOrUpdate(this.progressEventsSource.getValue(), message);
        this.progressEventsSource.next(data);
        break;
      case 'ended':
        data = this.progressEventsSource.getValue();
        data = data.filter(m => m.name !== message.name);
        this.progressEventsSource.next(data);
        this.activeEvents = Math.max(this.activeEvents - 1, 0);
        this.cdRef.markForCheck();
        break;
      default:
        break;
    }
  }

  private mergeOrUpdate(data: NotificationProgressEvent[], message: NotificationProgressEvent) {
    const index = data.findIndex(m => m.name === message.name);
    // Sometimes we can receive 2 started on long running scans, so better to just treat as a merge then.
    if (index < 0) {
      data.push(message);
      this.activeEvents += 1;
      this.cdRef.markForCheck();
    } else {
      data[index] = message;
    }
    return data;
  }


  handleUpdateAvailableClick(message: NotificationProgressEvent) {
    if (this.updateNotificationModalRef != null) { return; }
    this.updateNotificationModalRef = this.modalService.open(UpdateNotificationModalComponent, { scrollable: true, size: 'lg' });
    this.updateNotificationModalRef.componentInstance.updateData = message.body as UpdateVersionEvent;
    this.updateNotificationModalRef.closed.subscribe(() => {
      this.updateNotificationModalRef = null;
    });
    this.updateNotificationModalRef.dismissed.subscribe(() => {
      this.updateNotificationModalRef = null;
    });
  }

  async seeMoreError(error: ErrorEvent) {
    const config = new ConfirmConfig();
    config.buttons = [
      {text: 'Dismiss', type: 'primary'},
      {text: 'Ok', type: 'secondary'},
    ];
    config.header = error.title;
    config.content = error.subTitle;
    var result = await this.confirmService.alert(error.subTitle || error.title, config);
    if (result) {
      this.removeError(error);
    }
  }

  removeError(error: ErrorEvent, event?: MouseEvent) {
    if (event) {
      event.stopPropagation();
      event.preventDefault();
    }
    let data = this.errorSource.getValue();
    data = data.filter(m => m !== error); 
    this.errorSource.next(data);
    this.activeEvents = Math.max(this.activeEvents - 1, 0);
    this.cdRef.markForCheck();
  }

  prettyPrintProgress(progress: number) {
    return Math.trunc(progress * 100);
  }
}
