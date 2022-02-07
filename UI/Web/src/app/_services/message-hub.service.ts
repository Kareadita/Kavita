import { EventEmitter, Injectable } from '@angular/core';
import { Router } from '@angular/router';
import { HubConnection, HubConnectionBuilder } from '@microsoft/signalr';
import { NgbModal, NgbModalRef } from '@ng-bootstrap/ng-bootstrap';
import { ToastrService } from 'ngx-toastr';
import { BehaviorSubject, ReplaySubject } from 'rxjs';
import { environment } from 'src/environments/environment';
import { UpdateNotificationModalComponent } from '../shared/update-notification/update-notification-modal.component';
import { RefreshMetadataEvent } from '../_models/events/refresh-metadata-event';
import { ProgressEvent } from '../_models/events/scan-library-progress-event';
import { ScanSeriesEvent } from '../_models/events/scan-series-event';
import { SeriesAddedEvent } from '../_models/events/series-added-event';
import { User } from '../_models/user';

export enum EVENTS {
  UpdateAvailable = 'UpdateAvailable',
  ScanSeries = 'ScanSeries',
  RefreshMetadataProgress = 'RefreshMetadataProgress',
  SeriesAdded = 'SeriesAdded',
  SeriesRemoved = 'SeriesRemoved',
  ScanLibraryProgress = 'ScanLibraryProgress',
  OnlineUsers = 'OnlineUsers',
  SeriesAddedToCollection = 'SeriesAddedToCollection',
  ScanLibraryError = 'ScanLibraryError',
  BackupDatabaseProgress = 'BackupDatabaseProgress',
  CleanupProgress = 'CleanupProgress',
  DownloadProgress = 'DownloadProgress',
  /**
   * A cover is updated
   */
  CoverUpdate = 'CoverUpdate'
}

export interface Message<T> {
  event: EVENTS;
  payload: T;
}

@Injectable({
  providedIn: 'root'
})
export class MessageHubService {
  hubUrl = environment.hubUrl;
  private hubConnection!: HubConnection;

  private messagesSource = new ReplaySubject<Message<any>>(1);
  public messages$ = this.messagesSource.asObservable();

  private onlineUsersSource = new BehaviorSubject<string[]>([]);
  onlineUsers$ = this.onlineUsersSource.asObservable();

  public scanSeries: EventEmitter<ScanSeriesEvent> = new EventEmitter<ScanSeriesEvent>();
  public scanLibrary: EventEmitter<ProgressEvent> = new EventEmitter<ProgressEvent>(); // TODO: Refactor this name to be generic
  public seriesAdded: EventEmitter<SeriesAddedEvent> = new EventEmitter<SeriesAddedEvent>();

  isAdmin: boolean = false;

  constructor(private toastr: ToastrService, private router: Router) {
    
  }

  createHubConnection(user: User, isAdmin: boolean) {
    this.isAdmin = isAdmin;

    this.hubConnection = new HubConnectionBuilder()
      .withUrl(this.hubUrl + 'messages', {
        accessTokenFactory: () => user.token
      })
      .withAutomaticReconnect()
      .build();

    this.hubConnection
    .start()
    .catch(err => console.error(err));

    this.hubConnection.on(EVENTS.OnlineUsers, (usernames: string[]) => {
      this.onlineUsersSource.next(usernames);
    });


    this.hubConnection.on(EVENTS.ScanSeries, resp => {
      this.messagesSource.next({
        event: EVENTS.ScanSeries,
        payload: resp.body
      });
      this.scanSeries.emit(resp.body);
    });

    this.hubConnection.on(EVENTS.ScanLibraryProgress, resp => {
      this.messagesSource.next({
        event: EVENTS.ScanLibraryProgress,
        payload: resp.body
      });
      this.scanLibrary.emit(resp.body);
    });

    this.hubConnection.on(EVENTS.BackupDatabaseProgress, resp => {
      this.messagesSource.next({
        event: EVENTS.BackupDatabaseProgress,
        payload: resp.body
      });
    });

    this.hubConnection.on(EVENTS.CleanupProgress, resp => {
      this.messagesSource.next({
        event: EVENTS.CleanupProgress,
        payload: resp.body
      });
    });

    this.hubConnection.on(EVENTS.DownloadProgress, resp => {
      this.messagesSource.next({
        event: EVENTS.DownloadProgress,
        payload: resp.body
      });
    });

    this.hubConnection.on(EVENTS.RefreshMetadataProgress, resp => {
      this.messagesSource.next({
        event: EVENTS.RefreshMetadataProgress,
        payload: resp.body
      });
    });

    this.hubConnection.on(EVENTS.SeriesAddedToCollection, resp => {
      this.messagesSource.next({
        event: EVENTS.SeriesAddedToCollection,
        payload: resp.body
      });
    });

    this.hubConnection.on(EVENTS.ScanLibraryError, resp => {
      this.messagesSource.next({
        event: EVENTS.ScanLibraryError,
        payload: resp.body
      });
      if (this.isAdmin) {
        this.toastr.error('Library Scan had a critical error. Some series were not saved. Check logs');
      }
    });

    this.hubConnection.on(EVENTS.SeriesAdded, resp => {
      this.messagesSource.next({
        event: EVENTS.SeriesAdded,
        payload: resp.body
      });
      this.seriesAdded.emit(resp.body);
    });

    this.hubConnection.on(EVENTS.SeriesRemoved, resp => {
      this.messagesSource.next({
        event: EVENTS.SeriesRemoved,
        payload: resp.body
      });
    });

    // this.hubConnection.on(EVENTS.RefreshMetadata, resp => {
    //   this.messagesSource.next({
    //     event: EVENTS.RefreshMetadata,
    //     payload: resp.body
    //   });
    //   this.refreshMetadata.emit(resp.body); // TODO: Remove this
    // });

    this.hubConnection.on(EVENTS.CoverUpdate, resp => {
      this.messagesSource.next({
        event: EVENTS.CoverUpdate,
        payload: resp.body
      });
    });

    this.hubConnection.on(EVENTS.UpdateAvailable, resp => {
      this.messagesSource.next({
        event: EVENTS.UpdateAvailable,
        payload: resp.body
      });
    });
  }

  stopHubConnection() {
    if (this.hubConnection) {
      this.hubConnection.stop().catch(err => console.error(err));
    }
  }

  sendMessage(methodName: string, body?: any) {
    return this.hubConnection.invoke(methodName, body);
  }
  
}
