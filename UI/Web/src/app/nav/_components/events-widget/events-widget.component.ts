import {ChangeDetectionStrategy, Component, computed, DestroyRef, inject, input, OnInit, signal} from '@angular/core';
import {NgbModal, NgbModalRef, NgbPopover} from '@ng-bootstrap/ng-bootstrap';
import {ConfirmConfig} from 'src/app/shared/confirm-dialog/_models/confirm-config';
import {ConfirmService} from 'src/app/shared/confirm.service';
import {
  UpdateNotificationModalComponent
} from 'src/app/announcements/_components/update-notification/update-notification-modal.component';
import {DownloadService} from 'src/app/shared/_services/download.service';
import {ErrorEvent} from 'src/app/_models/events/error-event';
import {InfoEvent} from 'src/app/_models/events/info-event';
import {NotificationProgressEvent} from 'src/app/_models/events/notification-progress-event';
import {UpdateVersionEvent} from 'src/app/_models/events/update-version-event';
import {User} from 'src/app/_models/user/user';
import {AccountService} from 'src/app/_services/account.service';
import {EVENTS, Message, MessageHubService} from 'src/app/_services/message-hub.service';
import {takeUntilDestroyed, toSignal} from "@angular/core/rxjs-interop";
import {SentenceCasePipe} from '../../../_pipes/sentence-case.pipe';
import {NgClass, NgStyle} from '@angular/common';
import {TranslocoDirective} from "@jsverse/transloco";
import {DefaultModalOptions} from "../../../_models/default-modal-options";
import {RouterLink} from "@angular/router";
import {ReadingSessionUpdateEvent} from "../../../_models/events/reading-session-close-event";

@Component({
  selector: 'app-nav-events-toggle',
  templateUrl: './events-widget.component.html',
  styleUrls: ['./events-widget.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [NgClass, NgbPopover, NgStyle, SentenceCasePipe, TranslocoDirective, RouterLink]
})
export class EventsWidgetComponent implements OnInit {
  public readonly downloadService = inject(DownloadService);
  public readonly messageHub = inject(MessageHubService);
  private readonly modalService = inject(NgbModal);
  protected readonly accountService = inject(AccountService);
  private readonly confirmService = inject(ConfirmService);
  private readonly destroyRef = inject(DestroyRef);

  readonly user = input.required<User>();

  /** Progress events (Event Type: 'started', 'ended', 'updated' that have progress property) */
  readonly progressEvents = signal<NotificationProgressEvent[]>([]);
  readonly singleUpdates = signal<NotificationProgressEvent[]>([]);
  readonly errors = signal<ErrorEvent[]>([]);
  readonly infos = signal<InfoEvent[]>([]);
  readonly activeReadingSessions = signal<Set<number>>(new Set());

  private updateNotificationModalRef: NgbModalRef | null = null;

  /**
   * Does not include active reading sessions
   */
  readonly activeEvents = computed(() => {
    return this.progressEvents().length
      + this.singleUpdates().length
      + this.errors().length
      + this.infos().length;
  });

  /** Intercepts from Single Updates to show an extra indicator to the user */
  readonly updateAvailable = signal(false);

  readonly activeDownloads = toSignal(this.downloadService.activeDownloads$, { initialValue: [] });
  readonly onlineUsers = toSignal(this.messageHub.onlineUsers$, { initialValue: [] });


  ngOnInit(): void {
    this.messageHub.messages$.pipe(takeUntilDestroyed(this.destroyRef)).subscribe((event: Message<any>) => {
      if (event.event === EVENTS.NotificationProgress) {
        this.processNotificationProgressEvent(event);
      } else if (event.event === EVENTS.Error) {
        this.errors.update(values => [...values, event.payload as ErrorEvent]);
      } else if (event.event === EVENTS.Info) {
        this.infos.update(values => [...values, event.payload as InfoEvent]);
      } else if (event.event === EVENTS.UpdateAvailable) {
        this.handleUpdateAvailableClick(event.payload);
      } else if (event.event === EVENTS.ReadingSessionUpdate) {
        const data = event.payload as ReadingSessionUpdateEvent;
        this.activeReadingSessions.update(set => new Set([...set, data.sessionId]));
      } else if (event.event === EVENTS.ReadingSessionClose) {
        this.activeReadingSessions.update(set => {
          const newSet = new Set(set);
          newSet.delete(event.payload.sessionId);
          return newSet;
        });
      }
    });
  }

  processNotificationProgressEvent(event: Message<NotificationProgressEvent>) {
    const message = event.payload as NotificationProgressEvent;
    switch (event.payload.eventType) {
      case 'single':
        this.singleUpdates.update(values => [...values, message]);
        if (event.payload.name === EVENTS.UpdateAvailable) {
          this.updateAvailable.set(true);
        }
        break;
      case 'started':
      case 'updated':
        this.progressEvents.update(data => this.mergeOrUpdate(data, message));
        break;
      case 'ended':
        this.progressEvents.update(data => data.filter(m => m.name !== message.name));
        break;
      default:
        break;
    }
  }

  private mergeOrUpdate(data: NotificationProgressEvent[], message: NotificationProgressEvent) {
    // Sometimes we can receive 2 started on long-running scans, so better to just treat as a merge then.
    const index = data.findIndex(m => m.name === message.name);
    if (index < 0) {
      return [...data, message];
    }
    // Replace existing item immutably
    const newData = [...data];
    newData[index] = message;
    return newData;
  }


  handleUpdateAvailableClick(message: NotificationProgressEvent | UpdateVersionEvent) {
    if (this.updateNotificationModalRef != null) { return; }
    this.updateNotificationModalRef = this.modalService.open(UpdateNotificationModalComponent, DefaultModalOptions);
    if (message.hasOwnProperty('body')) {
      this.updateNotificationModalRef.componentInstance.updateData = (message as NotificationProgressEvent).body as UpdateVersionEvent;
    } else {
      this.updateNotificationModalRef.componentInstance.updateData = message as UpdateVersionEvent;
    }

    this.updateNotificationModalRef.closed.subscribe(() => {
      this.updateNotificationModalRef = null;
    });
    this.updateNotificationModalRef.dismissed.subscribe(() => {
      this.updateNotificationModalRef = null;
    });
  }

  async seeMore(event: ErrorEvent | InfoEvent) {
    const config = new ConfirmConfig();
    if (event.name === EVENTS.Error) {
      config.buttons = [
        {text: 'Ok', type: 'secondary'},
        {text: 'Dismiss', type: 'primary'}
      ];
    } else {
      config.buttons = [
        {text: 'Ok', type: 'primary'},
      ];
    }
    config.header = event.title;
    config.content = event.subTitle;
    const result = await this.confirmService.alert(event.subTitle || event.title, config);
    if (result) {
      this.removeErrorOrInfo(event);
    }
  }

  clearAllErrorOrInfos() {
    this.infos.set([]);
    this.errors.set([]);
  }

  removeErrorOrInfo(messageEvent: ErrorEvent | InfoEvent, event?: MouseEvent) {
    if (event) {
      event.stopPropagation();
      event.preventDefault();
    }
    if (messageEvent.name === EVENTS.Info) {
      this.infos.update(data => data.filter(m => m !== messageEvent));
    } else {
      this.errors.update(data => data.filter(m => m !== messageEvent));
    }
  }

  prettyPrintProgress(progress: number) {
    return Math.trunc(progress * 100);
  }

  protected readonly EVENTS = EVENTS;
}
