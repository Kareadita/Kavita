import {inject, Pipe, PipeTransform} from '@angular/core';
import {TranslocoService} from '@jsverse/transloco';
import {MessageEventCode} from '../_models/events/core/message-event-code';
import {UtcToLocalTimePipe} from './utc-to-local-time.pipe';

const PREFIX = 'event-message-pipe';

export interface EventMessageSource {
  code: MessageEventCode | null;
  title: string;
  subTitle: string;
  body: unknown;
}

export type EventMessagePart = 'label' | 'description';

type MessageBody = Partial<{
  libraryName: string;
  seriesName: string;
  folders: string[];
  folder: string;
  path: string;
  filePath: string;
  scheduledForUtc: string;
}>;

type Params = Record<string, string | undefined>;

/**
 * Title and second line of a coded Info or Error. Falls back to the server's English Title / SubTitle when the code is
 * unknown or a value it needs is missing
 */
@Pipe({
  name: 'eventMessage',
  standalone: true
})
export class EventMessagePipe implements PipeTransform {
  private readonly translocoService = inject(TranslocoService);
  private readonly utcToLocalTimePipe = new UtcToLocalTimePipe();

  transform(source: EventMessageSource, part: EventMessagePart): string {
    const body = (source.body ?? {}) as MessageBody;
    return part === 'label' ? this.label(source, body) : this.description(source, body);
  }

  private label(source: EventMessageSource, body: MessageBody): string {
    const fallback = source.title;
    const seriesName = body.seriesName;
    const libraryName = body.libraryName;

    switch (source.code) {
      case MessageEventCode.RootFoldersInaccessible:
        return this.translate(`${PREFIX}.root-folders-inaccessible-label`, {}, fallback);
      case MessageEventCode.RootFoldersEmpty:
        return this.translate(`${PREFIX}.root-folders-empty-label`, {libraryName}, fallback);
      case MessageEventCode.SeriesCollision:
        return this.translate(`${PREFIX}.series-collision-label`, {seriesName, libraryName}, fallback);
      case MessageEventCode.FilesOutsideFolder:
        return this.translate(`${PREFIX}.files-outside-folder-label`, {seriesName}, fallback);
      case MessageEventCode.ScanSeriesNoRoot:
        return this.translate(`${PREFIX}.scan-series-no-root-label`, {seriesName}, fallback);
      case MessageEventCode.ScanSeriesNotNested:
        return this.translate(`${PREFIX}.scan-series-not-nested-label`, {seriesName}, fallback);
      case MessageEventCode.ScanSeriesNoFiles:
        return this.translate(`${PREFIX}.scan-series-no-files-label`, {seriesName}, fallback);
      case MessageEventCode.ScanSeriesFolderMissing:
        return this.translate(`${PREFIX}.scan-series-folder-missing-label`, {seriesName}, fallback);
      case MessageEventCode.ScanNoWork:
        return this.translate(`${PREFIX}.scan-no-work-label`, {seriesName}, fallback);
      case MessageEventCode.DbWriteFailed:
        return this.translate(`${PREFIX}.db-write-failed-label`, {seriesName}, fallback);
      case MessageEventCode.ScanLibrariesDelayed:
        return this.translate(`${PREFIX}.scan-libraries-delayed-label`, {}, fallback);
      case MessageEventCode.ScanLibraryDelayed:
        return this.translate(`${PREFIX}.scan-library-delayed-label`, {libraryName}, fallback);
      case MessageEventCode.ScanSeriesDelayed:
        return this.translate(`${PREFIX}.scan-series-delayed-label`, {seriesName}, fallback);
      case MessageEventCode.BackupFolderUnwritable:
        return this.translate(`${PREFIX}.backup-folder-unwritable-label`, {}, fallback);
      case MessageEventCode.BackupExists:
        return this.translate(`${PREFIX}.backup-exists-label`, {}, fallback);
      case MessageEventCode.CleanupOnHold:
        return this.translate(`${PREFIX}.cleanup-on-hold-label`, {}, fallback);
      case MessageEventCode.WordCountFailed:
        return this.translate(`${PREFIX}.word-count-failed-label`, {seriesName}, fallback);
      default:
        return fallback;
    }
  }

  private description(source: EventMessageSource, body: MessageBody): string {
    const fallback = source.subTitle;
    const time = body.scheduledForUtc ? this.utcToLocalTimePipe.transform(body.scheduledForUtc, 'shortTime') : undefined;

    switch (source.code) {
      case MessageEventCode.RootFoldersInaccessible:
        return this.translate(`${PREFIX}.root-folders-inaccessible-description`, {folders: body.folders?.join(', ')}, fallback);
      case MessageEventCode.RootFoldersEmpty:
        return this.translate(`${PREFIX}.root-folders-empty-description`, {}, fallback);
      case MessageEventCode.SeriesCollision:
        return this.translate(`${PREFIX}.series-collision-description`, {}, fallback);
      case MessageEventCode.FilesOutsideFolder:
        return this.translate(`${PREFIX}.files-outside-folder-description`, {}, fallback);
      case MessageEventCode.ScanSeriesNoRoot:
        return this.translate(`${PREFIX}.scan-series-no-root-description`, {}, fallback);
      case MessageEventCode.ScanSeriesNotNested:
        return this.translate(`${PREFIX}.scan-series-not-nested-description`, {}, fallback);
      case MessageEventCode.ScanSeriesNoFiles:
        return this.translate(`${PREFIX}.scan-series-no-files-description`, {}, fallback);
      case MessageEventCode.ScanSeriesFolderMissing:
        return this.translate(`${PREFIX}.scan-series-folder-missing-description`, {}, fallback);
      case MessageEventCode.ScanNoWork:
        return this.translate(`${PREFIX}.scan-no-work-description`, {}, fallback);
      case MessageEventCode.DbWriteFailed:
        // SubTitle is the raw exception message
        return source.subTitle;
      case MessageEventCode.ScanLibrariesDelayed:
      case MessageEventCode.ScanLibraryDelayed:
      case MessageEventCode.ScanSeriesDelayed:
        return this.translate(`${PREFIX}.scan-delayed-description`, {time}, fallback);
      case MessageEventCode.BackupFolderUnwritable:
        return this.translate(`${PREFIX}.backup-folder-unwritable-description`, {folder: body.folder}, fallback);
      case MessageEventCode.BackupExists:
        return this.translate(`${PREFIX}.backup-exists-description`, {path: body.path}, fallback);
      case MessageEventCode.CleanupOnHold:
        return this.translate(`${PREFIX}.cleanup-on-hold-description`, {}, fallback);
      case MessageEventCode.WordCountFailed:
        return this.translate(`${PREFIX}.word-count-failed-description`, {filePath: body.filePath}, fallback);
      default:
        return fallback;
    }
  }

  private translate(key: string, params: Params, fallback: string): string {
    if (Object.values(params).some(v => !v)) return fallback;
    return this.translocoService.translate(key, params);
  }

}
