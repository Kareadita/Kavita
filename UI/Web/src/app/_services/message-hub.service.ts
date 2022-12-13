import { Injectable } from '@angular/core';
import { Router } from '@angular/router';
import { HubConnection, HubConnectionBuilder } from '@microsoft/signalr';
import { ToastrService } from 'ngx-toastr';
import { BehaviorSubject, ReplaySubject } from 'rxjs';
import { environment } from 'src/environments/environment';
import { LibraryModifiedEvent } from '../_models/events/library-modified-event';
import { NotificationProgressEvent } from '../_models/events/notification-progress-event';
import { ThemeProgressEvent } from '../_models/events/theme-progress-event';
import { UserUpdateEvent } from '../_models/events/user-update-event';
import { User } from '../_models/user';

export enum EVENTS {
  UpdateAvailable = 'UpdateAvailable',
  ScanSeries = 'ScanSeries',
  SeriesAdded = 'SeriesAdded',
  SeriesRemoved = 'SeriesRemoved',
  ScanLibraryProgress = 'ScanLibraryProgress',
  OnlineUsers = 'OnlineUsers',
  SeriesAddedToCollection = 'SeriesAddedToCollection',
  /**
   * A generic error that occurs during operations on the server
   */
  Error = 'Error',
  BackupDatabaseProgress = 'BackupDatabaseProgress',
  /**
   * A subtype of NotificationProgress that represents maintenance cleanup on server-owned resources
   */
  CleanupProgress = 'CleanupProgress',
  /**
   * A subtype of NotificationProgress that represnts a user downloading a file or group of files.
   * Note: In v0.5.5, this is being replaced by an inbrowser experience. The message is changed and this will be moved to dashboard view once built
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
   * A cover is updated
   */
  CoverUpdate = 'CoverUpdate',
  /**
   * A subtype of NotificationProgress that represents a file being processed for cover image extraction
   */
  CoverUpdateProgress = 'CoverUpdateProgress',
   /**
    * A library is created or removed from the instance
    */
  LibraryModified = 'LibraryModified',
   /**
    * A user updates an entities read progress
    */
  UserProgressUpdate = 'UserProgressUpdate',
   /**
    * A user updates account or preferences
    */
  UserUpdate = 'UserUpdate',
   /**
    * When bulk bookmarks are being converted
    */
  ConvertBookmarksProgress = 'ConvertBookmarksProgress',
   /**
    * When files are being scanned to calculate word count
    */
  WordCountAnalyzerProgress = 'WordCountAnalyzerProgress',
   /**
    * When the user needs to be informed, but it's not a big deal
    */
  Info = 'Info',
   /**
    * A user is sending files to their device
    */
  SendingToDevice = 'SendingToDevice',
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

    this.hubConnection.on(EVENTS.ConvertBookmarksProgress, resp => {
      this.messagesSource.next({
        event: EVENTS.ConvertBookmarksProgress,
        payload: resp.body
      });
    });

    this.hubConnection.on(EVENTS.WordCountAnalyzerProgress, resp => {
      this.messagesSource.next({
        event: EVENTS.WordCountAnalyzerProgress,
        payload: resp.body
      });
    });

    this.hubConnection.on(EVENTS.LibraryModified, resp => {
      this.messagesSource.next({
        event: EVENTS.LibraryModified,
        payload: resp.body as LibraryModifiedEvent
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

    this.hubConnection.on(EVENTS.SeriesAddedToCollection, resp => {
      this.messagesSource.next({
        event: EVENTS.SeriesAddedToCollection,
        payload: resp.body
      });
    });

    this.hubConnection.on(EVENTS.UserProgressUpdate, resp => {
      this.messagesSource.next({
        event: EVENTS.UserProgressUpdate,
        payload: resp.body
      });
    });

    this.hubConnection.on(EVENTS.UserUpdate, resp => {
      this.messagesSource.next({
        event: EVENTS.UserUpdate,
        payload: resp.body as UserUpdateEvent
      });
    });

    this.hubConnection.on(EVENTS.Error, resp => {
      this.messagesSource.next({
        event: EVENTS.Error,
        payload: resp.body
      });
    });

    this.hubConnection.on(EVENTS.Info, resp => {
      this.messagesSource.next({
        event: EVENTS.Info,
        payload: resp.body
      });
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

    this.hubConnection.on(EVENTS.SendingToDevice, resp => {
      this.messagesSource.next({
        event: EVENTS.SendingToDevice,
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
