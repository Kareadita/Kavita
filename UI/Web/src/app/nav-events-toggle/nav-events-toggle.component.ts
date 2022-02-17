import { Component, Input, OnDestroy, OnInit, ViewChild } from '@angular/core';
import { NgbModal, NgbModalRef, NgbPopover } from '@ng-bootstrap/ng-bootstrap';
import { BehaviorSubject, Subject } from 'rxjs';
import { takeUntil } from 'rxjs/operators';
import { UpdateNotificationModalComponent } from '../shared/update-notification/update-notification-modal.component';
import { NotificationProgressEvent } from '../_models/events/notification-progress-event';
import { ProgressEvent } from '../_models/events/progress-event';
import { UpdateVersionEvent } from '../_models/events/update-version-event';
import { User } from '../_models/user';
import { AccountService } from '../_services/account.service';
import { LibraryService } from '../_services/library.service';
import { EVENTS, Message, MessageHubService } from '../_services/message-hub.service';

interface ProcessedEvent {
  eventType: string;
  timestamp?: string;
  progress: number;
  libraryId: number;
  libraryName: string;
}



// TODO: Rename this to events widget
@Component({
  selector: 'app-nav-events-toggle',
  templateUrl: './nav-events-toggle.component.html',
  styleUrls: ['./nav-events-toggle.component.scss']
})
export class NavEventsToggleComponent implements OnInit, OnDestroy {

  @Input() user!: User;

  @ViewChild('popContent', {static: true}) popover!: NgbPopover;

  isAdmin: boolean = false; // TODO: Make this observable listener

  private readonly onDestroy = new Subject<void>();

  /**
   * Progress events (Event Type: 'started', 'ended', 'updated' that have progress property)
   */
  progressEventsSource = new BehaviorSubject<NotificationProgressEvent[]>([]);
  progressEvents$ = this.progressEventsSource.asObservable();

  singleUpdateSource = new BehaviorSubject<NotificationProgressEvent[]>([]);
  singleUpdates$ = this.singleUpdateSource.asObservable();

  //updateAvailable: boolean = false;
  //updateBody!: UpdateVersionEvent;
  private updateNotificationModalRef: NgbModalRef | null = null;

  activeEvents: number = 0;

  // Debug code
  updates: any = {}; // TODO: Remove the updates for progressEvents

  get updateEvents(): Array<NotificationProgressEvent> {
    return Object.values(this.updates);
  }

  get EVENTS() {
    return EVENTS;
  }

  constructor(public messageHub: MessageHubService, private libraryService: LibraryService, private modalService: NgbModal, private accountService: AccountService) { }

  ngOnDestroy(): void {
    this.onDestroy.next();
    this.onDestroy.complete();
    this.progressEventsSource.complete();
    this.singleUpdateSource.complete();
  }

  ngOnInit(): void {
    this.messageHub.messages$.pipe(takeUntil(this.onDestroy)).subscribe(event => {
      console.log(event.event);
      this.popover?.open();
      if (event.event.endsWith('error')) {
        // TODO: Show an error handle
      } else if (event.event === EVENTS.NotificationProgress) {
        this.processNotificationProgressEvent(event);
      }
    });
    this.accountService.currentUser$.pipe(takeUntil(this.onDestroy)).subscribe(user => {
      if (user) {
        this.isAdmin = this.accountService.hasAdminRole(user);
      } else {
        this.isAdmin = false;
      }
    });
  }

  processNotificationProgressEvent(event: Message<NotificationProgressEvent>) {
    const message = event.payload as NotificationProgressEvent;
    console.log('Notification Progress Event: ', event.event, message);
    console.log('Type: ', message.name);
    let data;

    switch (event.payload.eventType) {
      case 'single':
        const values = this.singleUpdateSource.value;
        values.push(message);
        this.singleUpdateSource.next(values);
        this.activeEvents += 1;
        break;
      case 'started':
        this.updates[message.name] = message;
        data = this.progressEventsSource.value;
        data.push(message);
        this.singleUpdateSource.next(data);
        console.log('Started: ', message.name);
        this.activeEvents += 1;
        break;
      case 'updated':
        this.updates[message.name] = message;
        data = this.progressEventsSource.value;
        const index = data.findIndex(m => m.name === message.name);
        data[index] = message;
        this.singleUpdateSource.next(data);
        console.log('Updated: ', message.name);
        break;
      case 'ended':
        delete this.updates[message.name];
        data = this.progressEventsSource.value;
        data = data.filter(m => m.name !== message.name);
        this.singleUpdateSource.next(data);
        console.log('Ended: ', message.name);
        this.activeEvents -= 1;
        break;
      default:
        break;
    }

    console.log('Active Events: ', this.activeEvents);

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

  prettyPrintProgress(progress: number) {
    return Math.trunc(progress * 100);
  }
}
