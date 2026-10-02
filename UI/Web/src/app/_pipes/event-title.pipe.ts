import {inject, Pipe, PipeTransform} from '@angular/core';
import {TranslocoService} from '@jsverse/transloco';
import {EVENTS} from '../_services/message-hub.service';
import {ScrobbleProvider} from '../_services/scrobbling.service';
import {ScrobbleProviderNamePipe} from './scrobble-provider-name.pipe';

const PREFIX = 'event-title-pipe';

export interface EventTitleSource {
  name: string;
  title: string;
  body: unknown;
}

type TitleBody = Partial<{
  libraryId: number;
  libraryName: string;
  collectionName: string;
  downloadName: string;
  themeName: string;
  updateVersion: string;
  provider: ScrobbleProvider;
}>;

/**
 * Row title by message name. Falls back to the server's English title when the name is unknown or a value it needs is missing
 */
@Pipe({
  name: 'eventTitle',
  standalone: true
})
export class EventTitlePipe implements PipeTransform {
  private readonly translocoService = inject(TranslocoService);
  private readonly providerNamePipe = new ScrobbleProviderNamePipe();

  /**
   * @param ended Scan rows read "Scanned" instead of "Scanning"
   * @param libraryNames From LibraryService.getLibraryNames(), for bodies that only carry libraryId
   */
  transform(source: EventTitleSource, ended: boolean = false, libraryNames?: Record<number, string>): string {
    const body = (source.body ?? {}) as TitleBody;
    const libraryName = body.libraryName || (body.libraryId != null ? libraryNames?.[body.libraryId] : undefined);

    switch (source.name as EVENTS) {
      case EVENTS.FileScanProgress:
      case EVENTS.ScanProgress:
        if (!libraryName) return source.title;
        return ended
          ? this.translocoService.translate(`${PREFIX}.scan-done`, {libraryName})
          : this.translocoService.translate(`${PREFIX}.scan-progress`, {libraryName});
      case EVENTS.CoverUpdateProgress:
        if (!libraryName) return source.title;
        return this.translocoService.translate(`${PREFIX}.cover-update-progress`, {libraryName});
      case EVENTS.WordCountAnalyzerProgress:
        if (!libraryName) return source.title;
        return this.translocoService.translate(`${PREFIX}.word-count-analyzer-progress`, {libraryName});
      case EVENTS.BackupDatabaseProgress:
        return this.translocoService.translate(`${PREFIX}.backup-database-progress`);
      case EVENTS.CleanupProgress:
        return this.translocoService.translate(`${PREFIX}.cleanup-progress`);
      case EVENTS.ConvertBookmarksProgress:
        return this.translocoService.translate(`${PREFIX}.convert-bookmarks-progress`);
      case EVENTS.ConvertCoversProgress:
        return this.translocoService.translate(`${PREFIX}.convert-covers-progress`);
      case EVENTS.SmartCollectionSync:
        if (!body.collectionName) return source.title;
        return this.translocoService.translate(`${PREFIX}.smart-collection-sync`, {collectionName: body.collectionName});
      case EVENTS.RerunMetadataMappingsProgress:
        return this.translocoService.translate(`${PREFIX}.rerun-metadata-mappings-progress`);
      case EVENTS.SendingToDevice:
        return this.translocoService.translate(`${PREFIX}.sending-to-device`);
      case EVENTS.DownloadProgress:
        if (!body.downloadName) return source.title;
        return this.translocoService.translate(`${PREFIX}.download-progress`, {downloadName: body.downloadName});
      case EVENTS.SiteThemeProgress:
        if (!body.themeName) return source.title;
        return this.translocoService.translate(`${PREFIX}.site-theme-progress`, {themeName: body.themeName});
      case EVENTS.BookThemeProgress:
        if (!body.themeName) return source.title;
        return this.translocoService.translate(`${PREFIX}.book-theme-progress`, {themeName: body.themeName});
      case EVENTS.UpdateAvailable:
        if (!body.updateVersion) return source.title;
        return this.translocoService.translate(`${PREFIX}.update-available`, {version: body.updateVersion});
      case EVENTS.ScrobblingKeyExpired: {
        const provider = body.provider != null ? this.providerNamePipe.transform(body.provider) : undefined;
        if (!provider) return source.title;
        return this.translocoService.translate(`${PREFIX}.scrobbling-key-expired`, {provider});
      }
      case EVENTS.ExternalMatchRateLimitError:
        return this.translocoService.translate(`${PREFIX}.external-match-rate-limit`);
      default:
        return source.title;
    }
  }

}
