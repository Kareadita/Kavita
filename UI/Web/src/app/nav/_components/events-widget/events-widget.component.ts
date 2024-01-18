import {
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component,
  DestroyRef,
  inject,
  Input,
  OnDestroy,
  OnInit
} from '@angular/core';
import { NgbModal, NgbModalRef, NgbPopover } from '@ng-bootstrap/ng-bootstrap';
import { BehaviorSubject, Observable, of } from 'rxjs';
import { map, shareReplay } from 'rxjs/operators';
import { ConfirmConfig } from 'src/app/shared/confirm-dialog/_models/confirm-config';
import { ConfirmService } from 'src/app/shared/confirm.service';
import { UpdateNotificationModalComponent } from 'src/app/shared/update-notification/update-notification-modal.component';
import { DownloadService } from 'src/app/shared/_services/download.service';
import { ErrorEvent } from 'src/app/_models/events/error-event';
import { InfoEvent } from 'src/app/_models/events/info-event';
import { NotificationProgressEvent } from 'src/app/_models/events/notification-progress-event';
import { UpdateVersionEvent } from 'src/app/_models/events/update-version-event';
import { User } from 'src/app/_models/user';
import { AccountService } from 'src/app/_services/account.service';
import { EVENTS, Message, MessageHubService } from 'src/app/_services/message-hub.service';
import {takeUntilDestroyed} from "@angular/core/rxjs-interop";
import { SentenceCasePipe } from '../../../_pipes/sentence-case.pipe';
import { CircularLoaderComponent } from '../../../shared/circular-loader/circular-loader.component';
import { NgIf, NgClass, NgStyle, NgFor, AsyncPipe } from '@angular/common';
import {TranslocoDirective} from "@ngneat/transloco";

@Component({
    selector: 'app-nav-events-toggle',
    templateUrl: './events-widget.component.html',
    styleUrls: ['./events-widget.component.scss'],
    changeDetection: ChangeDetectionStrategy.OnPush,
    standalone: true,
  imports: [NgIf, NgClass, NgbPopover, NgStyle, CircularLoaderComponent, NgFor, AsyncPipe, SentenceCasePipe, TranslocoDirective]
})
export class EventsWidgetComponent implements OnInit, OnDestroy {
  @Input({required: true}) user!: User;
  private readonly destroyRef = inject(DestroyRef);

  isAdmin$: Observable<boolean> = of(false);

  /**
   * Progress events (Event Type: 'started', 'ended', 'updated' that have progress property)
   */
  progressEventsSource = new BehaviorSubject<NotificationProgressEvent[]>([]);
  progressEvents$ = this.progressEventsSource.asObservable();

  singleUpdateSource = new BehaviorSubject<NotificationProgressEvent[]>([]);
  singleUpdates$ = this.singleUpdateSource.asObservable();

  errorSource = new BehaviorSubject<ErrorEvent[]>([]);
  errors$ = this.errorSource.asObservable();

  infoSource = new BehaviorSubject<InfoEvent[]>([]);
  infos$ = this.infoSource.asObservable();

  private updateNotificationModalRef: NgbModalRef | null = null;

  activeEvents: number = 0;

  debugMode: boolean = false;

  protected readonly EVENTS = EVENTS;

  public readonly downloadService = inject(DownloadService);

  constructor(public messageHub: MessageHubService, private modalService: NgbModal,
    private accountService: AccountService, private confirmService: ConfirmService,
    private readonly cdRef: ChangeDetectorRef) {
    }

  ngOnDestroy(): void {
    this.progressEventsSource.complete();
    this.singleUpdateSource.complete();
    this.errorSource.complete();
  }

  ngOnInit(): void {
    this.messageHub.messages$.pipe(takeUntilDestroyed(this.destroyRef)).subscribe((event: Message<NotificationProgressEvent>) => {
      if (event.event === EVENTS.NotificationProgress) {
        this.processNotificationProgressEvent(event);
      } else if (event.event === EVENTS.Error) {
        const values = this.errorSource.getValue();
        values.push(event.payload as ErrorEvent);
        this.errorSource.next(values);
        this.activeEvents += 1;
        this.cdRef.markForCheck();
      } else if (event.event === EVENTS.Info) {
        const values = this.infoSource.getValue();
        values.push(event.payload as InfoEvent);
        this.infoSource.next(values);
        this.activeEvents += 1;
        this.cdRef.markForCheck();
      } else if (event.event === EVENTS.UpdateAvailable) {
        this.handleUpdateAvailableClick(event.payload);
      }
    });

    this.isAdmin$ = this.accountService.currentUser$.pipe(
      takeUntilDestroyed(this.destroyRef),
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


  handleUpdateAvailableClick(message: NotificationProgressEvent | UpdateVersionEvent) {
    if (this.updateNotificationModalRef != null) { return; }
    this.updateNotificationModalRef = this.modalService.open(UpdateNotificationModalComponent, { scrollable: true, size: 'lg' });
    if (message.hasOwnProperty('body')) {
      this.updateNotificationModalRef.componentInstance.updateData = (message as NotificationProgressEvent).body as UpdateVersionEvent;
    } else {
      this.updateNotificationModalRef.componentInstance.updateData = message as UpdateVersionEvent;
    }

    this.updateNotificationModalRef.closed.subscribe(() => {
      this.updateNotificationModalRef = null;
    });
    this.updateNotificationModalRef.dismissed.subscribe(() => {
      this.updateNotificationModalRef = null;
    });
  }

  async seeMore(event: ErrorEvent | InfoEvent) {
    const config = new ConfirmConfig();
    if (event.name === EVENTS.Error) {
      config.buttons = [
        {text: 'Ok', type: 'secondary'},
        {text: 'Dismiss', type: 'primary'}
      ];
    } else {
      config.buttons = [
        {text: 'Ok', type: 'primary'},
      ];
    }
    config.header = event.title;
    config.content = event.subTitle;
    const result = await this.confirmService.alert(event.subTitle || event.title, config);
    if (result) {
      this.removeErrorOrInfo(event);
    }
  }

  clearAllErrorOrInfos() {
    const infoCount = this.infoSource.getValue().length;
    const errorCount = this.errorSource.getValue().length;
    this.infoSource.next([]);
    this.errorSource.next([]);
    this.activeEvents -= Math.max(infoCount + errorCount, 0);
    this.cdRef.markForCheck();
  }

  removeErrorOrInfo(messageEvent: ErrorEvent | InfoEvent, event?: MouseEvent) {
    if (event) {
      event.stopPropagation();
      event.preventDefault();
    }
    let data = [];
    if (messageEvent.name === EVENTS.Info) {
      data = this.infoSource.getValue();
      data = data.filter(m => m !== messageEvent);
      this.infoSource.next(data);
    } else {
      data = this.errorSource.getValue();
      data = data.filter(m => m !== messageEvent);
      this.errorSource.next(data);
    }
    this.activeEvents = Math.max(this.activeEvents - 1, 0);
    this.cdRef.markForCheck();
  }

  prettyPrintProgress(progress: number) {
    return Math.trunc(progress * 100);
  }
}
