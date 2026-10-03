import {MessageEventCode} from '../core/message-event-code';
import {CodedEventBody} from './coded-event-body';
import {LibraryEventBody} from './library-event-body';
import {SeriesEventBody} from './series-event-body';
import {RootFoldersInaccessibleBody} from './root-folders-inaccessible-body';
import {SeriesCollisionBody} from './series-collision-body';
import {ScanLibrariesDelayedBody} from './scan-libraries-delayed-body';
import {ScanLibraryDelayedBody} from './scan-library-delayed-body';
import {ScanSeriesDelayedBody} from './scan-series-delayed-body';
import {BackupFolderUnwritableBody} from './backup-folder-unwritable-body';
import {BackupExistsBody} from './backup-exists-body';
import {WordCountFailedBody} from './word-count-failed-body';

/**
 * Body of a coded Info or Error, by code. Scan loop codes are left out, they ride on progress messages with their own bodies
 */
export interface CodedEventBodyMap {
  [MessageEventCode.RootFoldersInaccessible]: RootFoldersInaccessibleBody;
  [MessageEventCode.RootFoldersEmpty]: LibraryEventBody;
  [MessageEventCode.SeriesCollision]: SeriesCollisionBody;
  [MessageEventCode.FilesOutsideFolder]: SeriesEventBody;
  [MessageEventCode.ScanSeriesNoRoot]: SeriesEventBody;
  [MessageEventCode.ScanSeriesNotNested]: SeriesEventBody;
  [MessageEventCode.ScanSeriesNoFiles]: SeriesEventBody;
  [MessageEventCode.ScanSeriesFolderMissing]: SeriesEventBody;
  [MessageEventCode.ScanNoWork]: SeriesEventBody;
  [MessageEventCode.DbWriteFailed]: SeriesEventBody;
  [MessageEventCode.ScanLibrariesDelayed]: ScanLibrariesDelayedBody;
  [MessageEventCode.ScanLibraryDelayed]: ScanLibraryDelayedBody;
  [MessageEventCode.ScanSeriesDelayed]: ScanSeriesDelayedBody;
  [MessageEventCode.BackupFolderUnwritable]: BackupFolderUnwritableBody;
  [MessageEventCode.BackupExists]: BackupExistsBody;
  [MessageEventCode.CleanupOnHold]: CodedEventBody;
  [MessageEventCode.WordCountFailed]: WordCountFailedBody;
}
