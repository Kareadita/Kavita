import { Component, Input, OnDestroy, OnInit, ViewChild } from '@angular/core';
import { NgbModal, NgbModalRef, NgbPopover } from '@ng-bootstrap/ng-bootstrap';
import { BehaviorSubject, Subject } from 'rxjs';
import { debounceTime, take, takeUntil, throttleTime } from 'rxjs/operators';
import { UpdateNotificationModalComponent } from '../shared/update-notification/update-notification-modal.component';
import { NotificationProgressEvent } from '../_models/events/notification-progress-event';
import { UpdateVersionEvent } from '../_models/events/update-version-event';
import { User } from '../_models/user';
import { AccountService } from '../_services/account.service';
import { EVENTS, Message, MessageHubService } from '../_services/message-hub.service';




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

  private updateNotificationModalRef: NgbModalRef | null = null;

  activeEvents: number = 0;


  get EVENTS() {
    return EVENTS;
  }

  constructor(public messageHub: MessageHubService, private modalService: NgbModal, private accountService: AccountService) { }

  ngOnDestroy(): void {
    this.onDestroy.next();
    this.onDestroy.complete();
    this.progressEventsSource.complete();
    this.singleUpdateSource.complete();
  }

  ngOnInit(): void {
    // Debounce for testing. Kavita's too fast
    this.messageHub.messages$.pipe(takeUntil(this.onDestroy), debounceTime(50)).subscribe(event => {
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
    console.log('Notification Progress Event: ', event.event, message, event.payload.eventType);
    let data;

    switch (event.payload.eventType) {
      case 'single':
        const values = this.singleUpdateSource.getValue();
        values.push(message);
        this.singleUpdateSource.next(values);
        this.activeEvents += 1;
        break;
      case 'started':
        data = this.progressEventsSource.getValue();
        data.push(message);
        this.progressEventsSource.next(data);
        console.log('Started: ', message.name);
        this.activeEvents += 1;
        break;
      case 'updated':
        data = this.progressEventsSource.getValue();
        const index = data.findIndex(m => m.name === message.name);
        data[index] = message;
        this.progressEventsSource.next(data);
        console.log('Updated: ', message.name);
        break;
      case 'ended':
        data = this.progressEventsSource.getValue();
        data = data.filter(m => m.name !== message.name); // This does not work //  && m.title !== message.title
        this.progressEventsSource.next(data);
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
