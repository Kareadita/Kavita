import { Component, OnDestroy, OnInit } from '@angular/core';
import { BehaviorSubject, Subject } from 'rxjs';
import { takeUntil } from 'rxjs/operators';
import { ProgressEvent } from '../_models/events/scan-library-progress-event';
import { LibraryService } from '../_services/library.service';
import { EVENTS, Message, MessageHubService } from '../_services/message-hub.service';

interface ProcessedEvent {
  eventType: string;
  timestamp?: string;
  progress: number;
  libraryId: number;
  libraryName: string;
}

@Component({
  selector: 'app-nav-events-toggle',
  templateUrl: './nav-events-toggle.component.html',
  styleUrls: ['./nav-events-toggle.component.scss']
})
export class NavEventsToggleComponent implements OnInit, OnDestroy {

  private readonly onDestroy = new Subject<void>();

  /**
   * Events that come through and are merged (ie progress event gets merged into a progress event)
   */
  private progressEventsSource = new BehaviorSubject<ProcessedEvent[]>([]);
  progressEvents$ = this.progressEventsSource.asObservable();

  private tasksEventsSource = new BehaviorSubject<ProcessedEvent[]>([]);
  taskEvents$ = this.tasksEventsSource.asObservable();

  constructor(private messageHub: MessageHubService, private libraryService: LibraryService) { }
  
  ngOnDestroy(): void {
    this.onDestroy.next();
    this.onDestroy.complete();
  }

  ngOnInit(): void {
    this.messageHub.messages$.pipe(takeUntil(this.onDestroy)).subscribe(event => {
      if (event.event === EVENTS.ScanLibraryProgress || event.event === EVENTS.RefreshMetadataProgress) {
        this.processProgressEvent(event, event.event);
      }
    });
  }


  processProgressEvent(event: Message<ProgressEvent>, eventType: EVENTS.ScanLibraryProgress | EVENTS.RefreshMetadataProgress) {
    const scanEvent = event.payload as ProgressEvent;
    console.log(event.event, event.payload);


    this.libraryService.getLibraryNames().subscribe(names => {
      const data = this.progressEventsSource.getValue();
      const index = data.findIndex(item => item.eventType === eventType && item.libraryId === event.payload.libraryId);
      if (index >= 0) {
        console.log('Removing ', data[index]);
        data.splice(index, 1);
      }

      if (scanEvent.progress !== 1) {
        const newEvent = {eventType: eventType, timestamp: scanEvent.eventTime, progress: scanEvent.progress, libraryId: scanEvent.libraryId, libraryName: names[scanEvent.libraryId]};
        data.push(newEvent);
        console.log('Adding ', newEvent);
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
      case (EVENTS.RefreshMetadataProgress): return 'Refreshing '
      default: return eventType;
    }
  }

}
