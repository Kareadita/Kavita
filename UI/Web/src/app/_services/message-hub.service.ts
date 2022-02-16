import { EventEmitter, Injectable } from '@angular/core';
import { Router } from '@angular/router';
import { HubConnection, HubConnectionBuilder } from '@microsoft/signalr';
import { ToastrService } from 'ngx-toastr';
import { BehaviorSubject, ReplaySubject } from 'rxjs';
import { environment } from 'src/environments/environment';
import { ProgressEvent } from '../_models/events/scan-library-progress-event';
import { ScanSeriesEvent } from '../_models/events/scan-series-event';
import { SeriesAddedEvent } from '../_models/events/series-added-event';
import { SiteThemeProgressEvent } from '../_models/events/site-theme-progress-event';
import { User } from '../_models/user';

export enum EVENTS {
  UpdateAvailable = 'UpdateAvailable',
  ScanSeries = 'ScanSeries',
  RefreshMetadata = 'RefreshMetadata',
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
  NotificationProgress = 'NotificationProgress',
  FileScanProgress = 'FileScanProgress',
  /**
   * A custom user site theme is added or removed during a scan
   */
  SiteThemeProgress = 'SiteThemeProgress',
  /**
   * A cover is updated
   */
  CoverUpdate = 'CoverUpdate'
}

export interface Message<T> {
  event: EVENTS;
  payload: T;
}

export interface SignalRMessage {
  body: any;
  name: string;
  title: string;
  subTitle: string;
  eventType: 'single' | 'started' | 'updated' | 'ended';
  eventTime: string;
}

@Injectable({
  providedIn: 'root'
})
export class MessageHubService {
  hubUrl = environment.hubUrl;
  private hubConnection!: HubConnection;

  private messagesSource = new ReplaySubject<Message<any>>(1);
  private onlineUsersSource = new BehaviorSubject<string[]>([]);

  /**
   * Any events that come from the backend
   */
  public messages$ = this.messagesSource.asObservable();
  /**
   * Users that are online
   */
  public onlineUsers$ = this.onlineUsersSource.asObservable();


  isAdmin: boolean = false;

  constructor(private toastr: ToastrService, private router: Router) {

  }

  /**
   * Tests that an event is of the type passed
   * @param event 
   * @param eventType 
   * @returns 
   */
  public isEventType(event: Message<any>, eventType: EVENTS) {
    if (event.event == EVENTS.NotificationProgress) {
      const notification = event.payload as SignalRMessage;
      return notification.eventType.toLowerCase() == eventType.toLowerCase();
    } 
    return event.event === eventType;
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
    });

    this.hubConnection.on(EVENTS.ScanLibraryProgress, resp => {
      this.messagesSource.next({
        event: EVENTS.ScanLibraryProgress,
        payload: resp.body
      });
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

    this.hubConnection.on(EVENTS.NotificationProgress, (resp: SignalRMessage) => {
      this.messagesSource.next({
        event: EVENTS.NotificationProgress,
        payload: resp
      });




    });

    this.hubConnection.on(EVENTS.RefreshMetadataProgress, resp => {
      this.messagesSource.next({
        event: EVENTS.RefreshMetadataProgress,
        payload: resp.body
      });
    });

    this.hubConnection.on(EVENTS.SiteThemeProgress, resp => {
      this.messagesSource.next({
        event: EVENTS.SiteThemeProgress,
        payload: resp.body as SiteThemeProgressEvent
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
    });

    this.hubConnection.on(EVENTS.SeriesRemoved, resp => {
      this.messagesSource.next({
        event: EVENTS.SeriesRemoved,
        payload: resp.body
      });
    });

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
