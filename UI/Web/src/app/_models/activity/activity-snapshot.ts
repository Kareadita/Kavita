import {SignalRMessage} from '../events/core/signalr-message';
import {ScheduledScan} from './scheduled-scan';
import {UpcomingTask} from './upcoming-task';

export interface ActivitySnapshot {
  bootId: string;
  startedUtc: string;
  running: SignalRMessage[];
  /**
   * Soonest first, at most 20 of scheduledTotal
   */
  scheduled: ScheduledScan[];
  scheduledTotal: number;
  upcoming: UpcomingTask[];
}
