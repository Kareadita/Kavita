import {ChangeDetectionStrategy, Component, computed, inject, input, output, signal} from '@angular/core';
import {Router} from '@angular/router';
import {translate, TranslocoDirective} from '@jsverse/transloco';
import {ToastrService} from '@openng/ngx-toastr';
import {ActivityEntry} from '../../../_models/activity/activity-entry';
import {MessageEventPriority} from '../../../_models/events/core/message-event-priority';
import {MessageEventCode} from '../../../_models/events/core/message-event-code';
import {EventAction} from '../../../_models/events/event-action';
import {EVENTS} from '../../../_services/message-hub.service';
import {LibraryService} from '../../../_services/library.service';
import {VersionService} from '../../../_services/version.service';
import {ConfirmService} from '../../../shared/confirm.service';
import {ConfirmConfig} from '../../../shared/confirm-dialog/_models/confirm-config';
import {UpdateVersionEvent} from '../../../_models/events/update-version-event';
import {SettingsTabId} from '../../../sidenav/preference-nav/preference-nav.component';
import {EventMessagePipe} from '../../../_pipes/event-message.pipe';
import {EventTitlePipe} from '../../../_pipes/event-title.pipe';
import {EventActionPipe} from '../../../_pipes/event-action.pipe';
import {ActivityAgePipe} from '../../../_pipes/activity-age.pipe';

const SeriesCodes: (MessageEventCode | null)[] = [
  MessageEventCode.FilesOutsideFolder,
  MessageEventCode.ScanSeriesNoRoot,
  MessageEventCode.ScanSeriesNotNested,
  MessageEventCode.ScanSeriesNoFiles,
  MessageEventCode.ScanSeriesFolderMissing,
  MessageEventCode.ScanNoWork,
  MessageEventCode.DbWriteFailed,
  MessageEventCode.WordCountFailed,
];

@Component({
  selector: 'app-activity-entry-row',
  templateUrl: './activity-entry-row.component.html',
  styleUrl: './activity-entry-row.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [TranslocoDirective, EventActionPipe, ActivityAgePipe]
})
export class ActivityEntryRowComponent {
  private readonly router = inject(Router);
  private readonly libraryService = inject(LibraryService);
  private readonly versionService = inject(VersionService);
  private readonly confirmService = inject(ConfirmService);
  private readonly toastr = inject(ToastrService);
  private readonly eventMessagePipe = new EventMessagePipe();
  private readonly eventTitlePipe = new EventTitlePipe();

  readonly entry = input.required<ActivityEntry>();
  readonly now = input.required<number>();
  readonly libraryNames = input<Record<number, string>>();
  readonly pinned = input(false);
  readonly dismissed = output<string>();
  readonly navigated = output<void>();

  protected expanded = signal(false);

  protected readonly label = computed(() => {
    const entry = this.entry();
    return entry.code
      ? this.eventMessagePipe.transform(entry, 'label')
      : this.eventTitlePipe.transform(entry, false, this.libraryNames());
  });

  protected readonly description = computed(() => {
    const entry = this.entry();
    if (entry.code) return this.eventMessagePipe.transform(entry, 'description');

    switch (entry.name) {
      case EVENTS.ExternalMatchRateLimitError:
        return translate('events-widget.rate-limit-description', {count: entry.count});
      case EVENTS.UpdateAvailable:
      case EVENTS.ScrobblingKeyExpired:
        return '';
      default:
        return entry.subTitle;
    }
  });

  protected readonly tone = computed(() => {
    switch (this.entry().priority) {
      case MessageEventPriority.Error: return 'error';
      case MessageEventPriority.Action: return 'action';
      case MessageEventPriority.Info: return 'info';
      default: return '';
    }
  });

  protected readonly isUpdate = computed(() => this.entry().name === EVENTS.UpdateAvailable);
  protected readonly expandable = computed(() => this.entry().priority === MessageEventPriority.Error);

  protected readonly actions = computed(() => {
    const entry = this.entry();

    switch (entry.name) {
      case EVENTS.UpdateAvailable: return [EventAction.WhatsNew];
      case EVENTS.ScrobblingKeyExpired: return [EventAction.Reconnect];
      case EVENTS.ExternalMatchRateLimitError: return [EventAction.OpenMatching];
    }

    switch (entry.code) {
      case MessageEventCode.RootFoldersInaccessible:
      case MessageEventCode.RootFoldersEmpty:
        return entry.libraryId !== null ? [EventAction.Rescan] : [];
      case MessageEventCode.SeriesCollision:
        return [EventAction.Details];
    }

    if (SeriesCodes.includes(entry.code) && entry.libraryId !== null && entry.seriesId !== null) {
      return [EventAction.OpenSeries];
    }
    return [];
  });

  protected run(action: EventAction) {
    const entry = this.entry();

    switch (action) {
      case EventAction.WhatsNew:
        this.versionService.showUpdateModal('update-available', {update: entry.body as UpdateVersionEvent}, true);
        break;
      case EventAction.Reconnect:
        this.navigate(['settings'], SettingsTabId.Connections);
        break;
      case EventAction.OpenMatching:
        this.navigate(['settings'], SettingsTabId.MatchedMetadata);
        break;
      case EventAction.OpenSeries:
        this.navigate(['library', entry.libraryId, 'series', entry.seriesId]);
        break;
      case EventAction.Rescan:
        this.rescan(entry.libraryId!);
        break;
      case EventAction.Details:
        this.showDetails();
        break;
    }
  }

  private navigate(commands: unknown[], fragment?: string) {
    this.router.navigate(commands, {fragment});
    this.navigated.emit();
  }

  private rescan(libraryId: number) {
    this.libraryService.scan(libraryId).subscribe(() => {
      const name = this.libraryNames()?.[libraryId] ?? '';
      this.toastr.info(translate('toasts.scan-queued', {name}));
    });
  }

  /**
   * The series collision SubTitle is an HTML table of the clashing files
   */
  private async showDetails() {
    const config = new ConfirmConfig();
    config.header = this.label();
    config.content = this.entry().subTitle;
    config.buttons = [{text: translate('common.close'), type: 'primary'}];
    await this.confirmService.alert(undefined, config);
  }

}
