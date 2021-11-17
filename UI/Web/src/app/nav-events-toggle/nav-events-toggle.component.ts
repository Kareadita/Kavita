import { Component, Input, OnDestroy, OnInit } from '@angular/core';
import { BehaviorSubject, Subject } from 'rxjs';
import { takeUntil } from 'rxjs/operators';
import { ProgressEvent } from '../_models/events/scan-library-progress-event';
import { User } from '../_models/user';
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

@Component({
  selector: 'app-nav-events-toggle',
  templateUrl: './nav-events-toggle.component.html',
  styleUrls: ['./nav-events-toggle.component.scss']
})
export class NavEventsToggleComponent implements OnInit, OnDestroy {

  @Input() user!: User;

  private readonly onDestroy = new Subject<void>();

  /**
   * Events that come through and are merged (ie progress event gets merged into a progress event)
   */
  progressEventsSource = new BehaviorSubject<ProcessedEvent[]>([]);
  progressEvents$ = this.progressEventsSource.asObservable();

  constructor(private messageHub: MessageHubService, private libraryService: LibraryService) { }
  
  ngOnDestroy(): void {
    this.onDestroy.next();
    this.onDestroy.complete();
    this.progressEventsSource.complete();
  }

  ngOnInit(): void {
    this.messageHub.messages$.pipe(takeUntil(this.onDestroy)).subscribe(event => {
      if (event.event === EVENTS.ScanLibraryProgress || event.event === EVENTS.RefreshMetadataProgress || event.event === EVENTS.BackupDatabaseProgress  || event.event === EVENTS.CleanupProgress) {
        this.processProgressEvent(event, event.event);
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
        const newEvent = {eventType: eventType, timestamp: scanEvent.eventTime, progress: scanEvent.progress, libraryId: scanEvent.libraryId, libraryName};
        data.push(newEvent);
      }

      
      this.progressEventsSource.next(data);
    });
  }

  prettyPrintProgress(progress: number) {
    return Math.trunc(progress * 100);
  }

  prettyPrintEvent(eventType: string) {
    switch(eventType) {
      case (EVENTS.ScanLibraryProgress): return 'Scanning ';
      case (EVENTS.RefreshMetadataProgress): return 'Refreshing ';
      case (EVENTS.CleanupProgress): return 'Clearing Cache';
      case (EVENTS.BackupDatabaseProgress): return 'Backing up Database';
      default: return eventType;
    }
  }

}
