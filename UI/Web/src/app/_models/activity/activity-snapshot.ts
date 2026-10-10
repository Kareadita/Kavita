import {SignalRMessage} from '../events/core/signalr-message';
import {ScheduledScan} from './scheduled-scan';
import {ScheduledFolderScan} from './scheduled-folder-scan';
import {UpcomingTask} from './upcoming-task';
import {RecentJob} from './recent-job';

export interface ActivitySnapshot {
  bootId: string;
  startedUtc: string;
  running: SignalRMessage[];
  /**
   * Soonest first, at most 20 of scheduledTotal
   */
  scheduled: ScheduledScan[];
  scheduledTotal: number;
  /**
   * Folder changes waiting to be looked at, one per library and trigger
   */
  scheduledFolderScans: ScheduledFolderScan[];
  upcoming: UpcomingTask[];
  /**
   * Last 24 hours, newest first. Empty after a server restart
   */
  recentJobs: RecentJob[];
  /**
   * Info, Error and rate limit messages from the last 24 hours, newest first
   */
  recentEntries: SignalRMessage[];
}
