import {Injectable} from '@angular/core';
import {HubConnection, HubConnectionBuilder} from '@microsoft/signalr';
import {BehaviorSubject, ReplaySubject} from 'rxjs';
import {environment} from '../../environments/environment';
import {NotificationProgressEvent} from '../_models/events/notification-progress-event';
import {User} from '../_models/user/user';
import {SignalRMessage} from '../_models/events/core/signalr-message';
import {MessageMeta} from '../_models/events/core/message-meta';
import {toSignal} from "@angular/core/rxjs-interop";

export enum EVENTS {
  UpdateAvailable = 'UpdateAvailable',
  ScanSeries = 'ScanSeries',
  SeriesAdded = 'SeriesAdded',
  SeriesRemoved = 'SeriesRemoved',
  VolumeRemoved = 'VolumeRemoved',
  ChapterRemoved = 'ChapterRemoved',
  ScanLibraryProgress = 'ScanLibraryProgress',
  OnlineUsers = 'OnlineUsers',
  /**
   * When a Collection has been updated
   */
  CollectionUpdated = 'CollectionUpdated',
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
   * A subtype of NotificationProgress that represents a user downloading a file or group of files.
   * Note: In v0.5.5, this is being replaced by an in-browser experience. The message is changed and this will be moved to dashboard view once built
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
   * A subtype of NotificationProgress that represents a single series being processed (into the DB)
   */
  ScanProgress = 'ScanProgress',
  /**
   * A custom user site theme is added or removed during a scan
   */
  SiteThemeProgress = 'SiteThemeProgress',
  /**
   * A subtype of NotificationProgress for a book theme being processed
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
   * When bulk covers are being converted
   */
  ConvertCoversProgress = 'ConvertCoversProgress',
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
  /**
   * A scrobbling token has expired
   */
  ScrobblingKeyExpired = 'ScrobblingKeyExpired',
  /**
   * User's dashboard needs to be re-rendered
   */
  DashboardUpdate = 'DashboardUpdate',
  /**
   * User's sidenav needs to be re-rendered
   */
  SideNavUpdate = 'SideNavUpdate',
  /**
   * A Theme was updated and UI should refresh to get the latest version
   */
  SiteThemeUpdated = 'SiteThemeUpdated',
  /**
   * A Progress event when a smart collection is synchronizing
   */
  SmartCollectionSync = 'SmartCollectionSync',
  /**
   * A Person merged has been merged into another
   */
  PersonMerged = 'PersonMerged',
  /**
   * A Rate limit error was hit when matching a series with Kavita+
   */
  ExternalMatchRateLimitError = 'ExternalMatchRateLimitError',
  /**
   * Annotation is updated within the reader
   */
  AnnotationUpdate = 'AnnotationUpdate',
  /**
   * Reading Session close
   */
  ReadingSessionClose = 'ReadingSessionClose',
  /**
   * Reading Session Update
   */
  ReadingSessionUpdate = 'ReadingSessionUpdate',
  /**
   * Auth key has been rotated, created
   */
  AuthKeyUpdate = 'AuthKeyUpdate',
  /**
   * An Auth key has been deleted
   */
  AuthKeyDeleted = 'AuthKeyDeleted',
  /**
   * A Reading List was updated (like via Sync operation)
   */
  ReadingListUpdated = 'ReadingListUpdated',
  /**
   * A series was updated (E.x. K+ match)
   */
  SeriesUpdated = 'SeriesUpdated',
  /**
   * A scrobble provider has had their (authentication) details updated
   */
  ScrobbleProviderUpdated = 'ScrobbleProviderUpdated',
  /**
   * The K+ license info has updated
   */
  LicenseInfoUpdate = 'LicenseInfoUpdate',
  /**
   * The K+ Metadata for a series has been updated
   */
  ExternalMetadataUpdate = 'ExternalMetadataUpdate',
  /**
   * Progress event send after a batch completes
   */
  RerunMetadataMappingsProgress = 'RerunMetadataMappingsProgress',
}

export interface Message<T> {
  event: EVENTS;
  payload: T;
  /**
   * The envelope without its body. Missing on messages not built from a hub frame
   */
  meta?: MessageMeta;
}

const bodyPayloadEvents = [
  EVENTS.ScanSeries,
  EVENTS.LibraryModified,
  EVENTS.SiteThemeUpdated,
  EVENTS.DashboardUpdate,
  EVENTS.SideNavUpdate,
  EVENTS.ExternalMatchRateLimitError,
  EVENTS.AnnotationUpdate,
  EVENTS.ReadingSessionClose,
  EVENTS.ReadingSessionUpdate,
  EVENTS.CollectionUpdated,
  EVENTS.UserProgressUpdate,
  EVENTS.UserUpdate,
  EVENTS.Error,
  EVENTS.Info,
  EVENTS.SeriesAdded,
  EVENTS.SeriesRemoved,
  EVENTS.ChapterRemoved,
  EVENTS.VolumeRemoved,
  EVENTS.CoverUpdate,
  EVENTS.ReadingListUpdated,
  EVENTS.UpdateAvailable,
  EVENTS.ScrobblingKeyExpired,
  EVENTS.PersonMerged,
  EVENTS.AuthKeyUpdate,
  EVENTS.AuthKeyDeleted,
  EVENTS.SeriesUpdated,
  EVENTS.ScrobbleProviderUpdated,
  EVENTS.LicenseInfoUpdate,
  EVENTS.ExternalMetadataUpdate,
];

const envelopePayloadEvents = [
  EVENTS.NotificationProgress,
  EVENTS.DownloadProgress,
];


@Injectable({
  providedIn: 'root'
})
export class MessageHubService {
  hubUrl = environment.hubUrl;
  private hubConnection!: HubConnection;

  private messagesSource = new ReplaySubject<Message<any>>(1);
  private onlineUsersSource = new BehaviorSubject<string[]>([]); // UserNames
  private isConnectedSource =  new BehaviorSubject<boolean>(false);

    /**
   * Any events that come from the backend
   */
  public readonly messages$ = this.messagesSource.asObservable();
  public readonly messageSignal = toSignal(this.messages$);
  /**
   * Users that are online
   */
  public onlineUsers$ = this.onlineUsersSource.asObservable();
  public readonly onlineUsersSignal = toSignal(this.onlineUsers$);


  public readonly isConnectedSignal = toSignal(this.isConnectedSource);

  constructor() {}

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

  createHubConnection(user: User) {
    this.hubConnection = new HubConnectionBuilder()
      .withUrl(this.hubUrl + 'messages', {
        accessTokenFactory: () => user.token
      })
      .withAutomaticReconnect()
      .withStatefulReconnect()
      .build();

    this.hubConnection.onreconnecting(() => this.isConnectedSource.next(false));
    this.hubConnection.onreconnected(() => this.isConnectedSource.next(true));
    this.hubConnection.onclose(() => this.isConnectedSource.next(false));

    const started = this.hubConnection
      .start()
      .then(() => {
        // Only report connected once the handshake actually resolves
        this.isConnectedSource.next(true);
      })
      .catch(err => {
        console.error(err);
        this.isConnectedSource.next(false);
      });

    this.hubConnection.on(EVENTS.OnlineUsers, (usernames: string[]) => {
      this.onlineUsersSource.next(usernames);
    });

    bodyPayloadEvents.forEach(event => {
      this.hubConnection.on(event, (resp: SignalRMessage) => this.emit(event, resp, resp.body));
    });

    envelopePayloadEvents.forEach(event => {
      this.hubConnection.on(event, (resp: SignalRMessage) => this.emit(event, resp, resp));
    });

    return started;
  }

  private emit(event: EVENTS, resp: SignalRMessage, payload: unknown) {
    const {body, ...meta} = resp;
    this.messagesSource.next({event, payload, meta});
  }

  stopHubConnection() {
    if (this.hubConnection) {
      this.hubConnection.stop().catch(err => console.error(err));
      this.isConnectedSource.next(false);
    }
  }
}
