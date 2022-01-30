import { Component, Input, OnDestroy, OnInit } from '@angular/core';
import { NgbModal, NgbModalRef } from '@ng-bootstrap/ng-bootstrap';
import { BehaviorSubject, Subject } from 'rxjs';
import { takeUntil } from 'rxjs/operators';
import { UpdateNotificationModalComponent } from '../shared/update-notification/update-notification-modal.component';
import { ProgressEvent } from '../_models/events/scan-library-progress-event';
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

type ProgressType = EVENTS.ScanLibraryProgress | EVENTS.RefreshMetadataProgress | EVENTS.BackupDatabaseProgress | EVENTS.CleanupProgress;

const acceptedEvents = [EVENTS.ScanLibraryProgress, EVENTS.RefreshMetadataProgress, EVENTS.BackupDatabaseProgress, EVENTS.CleanupProgress, EVENTS.DownloadProgress];

@Component({
  selector: 'app-nav-events-toggle',
  templateUrl: './nav-events-toggle.component.html',
  styleUrls: ['./nav-events-toggle.component.scss']
})
export class NavEventsToggleComponent implements OnInit, OnDestroy {

  @Input() user!: User;
  isAdmin: boolean = false;

  private readonly onDestroy = new Subject<void>();

  /**
   * Events that come through and are merged (ie progress event gets merged into a progress event)
   */
  progressEventsSource = new BehaviorSubject<ProcessedEvent[]>([]);
  progressEvents$ = this.progressEventsSource.asObservable();

  updateAvailable: boolean = false;
  updateBody: any;
  private updateNotificationModalRef: NgbModalRef | null = null;

  constructor(private messageHub: MessageHubService, private libraryService: LibraryService, private modalService: NgbModal, private accountService: AccountService) { }
  
  ngOnDestroy(): void {
    this.onDestroy.next();
    this.onDestroy.complete();
    this.progressEventsSource.complete();
  }

  ngOnInit(): void {
    this.messageHub.messages$.pipe(takeUntil(this.onDestroy)).subscribe(event => {
      if (acceptedEvents.includes(event.event)) {
        this.processProgressEvent(event, event.event);
      } else if (event.event === EVENTS.UpdateAvailable) {
        this.updateAvailable = true;
        this.updateBody = event.payload;
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


  processProgressEvent(event: Message<ProgressEvent>, eventType: string) {
    const scanEvent = event.payload as ProgressEvent;
    console.log(event.event, event.payload);


    this.libraryService.getLibraryNames().subscribe(names => {
      const data = this.progressEventsSource.getValue();
      const index = data.findIndex(item => item.eventType === eventType && item.libraryId === event.payload.libraryId);
      if (index >= 0) {
        data.splice(index, 1);
      }

      if (scanEvent.progress !== 1) {
        const libraryName = names[scanEvent.libraryId] || '';
        const newEvent = {eventType: eventType, timestamp: scanEvent.eventTime, progress: scanEvent.progress, libraryId: scanEvent.libraryId, libraryName, rawBody: event.payload};
        data.push(newEvent);
      }

      
      this.progressEventsSource.next(data);
    });
  }

  handleUpdateAvailableClick() {
    if (this.updateNotificationModalRef != null) { return; }
    this.updateNotificationModalRef = this.modalService.open(UpdateNotificationModalComponent, { scrollable: true, size: 'lg' });
    this.updateNotificationModalRef.componentInstance.updateData = this.updateBody;
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

  prettyPrintEvent(eventType: string, event: any) {
    switch(eventType) {
      case (EVENTS.ScanLibraryProgress): return 'Scanning ';
      case (EVENTS.RefreshMetadataProgress): return 'Refreshing Covers for ';
      case (EVENTS.CleanupProgress): return 'Clearing Cache';
      case (EVENTS.BackupDatabaseProgress): return 'Backing up Database';
      case (EVENTS.DownloadProgress): return event.rawBody.userName.charAt(0).toUpperCase() + event.rawBody.userName.substr(1) + ' is downloading ' + event.rawBody.downloadName;
      default: return eventType;
    }
  }

}
