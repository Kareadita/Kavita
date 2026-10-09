import {ActivityEntry} from '../_models/activity/activity-entry';
import {ScheduledScan} from '../_models/activity/scheduled-scan';
import {MessageEventCode} from '../_models/events/core/message-event-code';

/**
 * Matches on the scan's target, not its time. A retime moves the job, and parked jobs share one time
 */
export function isDelayedEntryFor(entry: ActivityEntry, scan: ScheduledScan): boolean {
  switch (entry.code) {
    case MessageEventCode.ScanLibrariesDelayed:
      return scan.libraryId === null;
    case MessageEventCode.ScanLibraryDelayed:
      return scan.seriesId === null && scan.libraryId !== null && entry.libraryId === scan.libraryId;
    case MessageEventCode.ScanSeriesDelayed:
      return scan.seriesId !== null && entry.seriesId === scan.seriesId;
    default:
      return false;
  }
}
