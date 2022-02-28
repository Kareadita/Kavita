import { Injectable } from '@angular/core';
import { Router } from '@angular/router';
import { HubConnection, HubConnectionBuilder } from '@microsoft/signalr';
import { ToastrService } from 'ngx-toastr';
import { BehaviorSubject, ReplaySubject } from 'rxjs';
import { environment } from 'src/environments/environment';
import { NotificationProgressEvent } from '../_models/events/notification-progress-event';
import { ThemeProgressEvent } from '../_models/events/theme-progress-event';
import { User } from '../_models/user';

export enum EVENTS {
  UpdateAvailable = 'UpdateAvailable',
  ScanSeries = 'ScanSeries',
  SeriesAdded = 'SeriesAdded',
  SeriesRemoved = 'SeriesRemoved',
  ScanLibraryProgress = 'ScanLibraryProgress',
  OnlineUsers = 'OnlineUsers',
  SeriesAddedToCollection = 'SeriesAddedToCollection',
  ScanLibraryError = 'ScanLibraryError',
  BackupDatabaseProgress = 'BackupDatabaseProgress',
  /**
   * A subtype of NotificationProgress that represents maintenance cleanup on server-owned resources
   */
  CleanupProgress = 'CleanupProgress',
  /**
   * A subtype of NotificationProgress that represnts a user downloading a file or group of files
   */
  DownloadProgress = 'DownloadProgress',
  /**
   * A generic progress event 
   */
  NotificationProgress = 'NotificationProgress',
  /**
   * A subtype of NotificationProgress that represents the underlying file being processed during a scan
   */
  FileScanProgress = 'FileScanProgress',
  /**
   * A custom user site theme is added or removed during a scan
   */
  SiteThemeProgress = 'SiteThemeProgress',
  /**
   * A custom user book theme is added or removed during a scan
   */
  BookThemeProgress = 'BookThemeProgress',
  /**
   * A cover is updated
   */
  CoverUpdate = 'CoverUpdate',
  /**
   * A subtype of NotificationProgress that represents a file being processed for cover image extraction
   */
   CoverUpdateProgress = 'CoverUpdateProgress',
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
      const notification = event.payload as NotificationProgressEvent;
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


    this.hubConnection.on(EVENTS.NotificationProgress, (resp: NotificationProgressEvent) => {
      this.messagesSource.next({
        event: EVENTS.NotificationProgress,
        payload: resp
      });
    });

    this.hubConnection.on(EVENTS.SiteThemeProgress, resp => {
      this.messagesSource.next({
        event: EVENTS.SiteThemeProgress,
        payload: resp.body as ThemeProgressEvent
      });
    });

    this.hubConnection.on(EVENTS.BookThemeProgress, resp => {
      this.messagesSource.next({
        event: EVENTS.BookThemeProgress,
        payload: resp.body as ThemeProgressEvent
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
        // TODO: Just show the error, RBS is done in eventhub
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
