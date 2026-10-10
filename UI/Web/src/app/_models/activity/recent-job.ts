import {SignalRMessage} from '../events/core/signalr-message';
import {LibraryScanSummary} from './library-scan-summary';

export interface RecentJob {
  correlationId: string;
  startedUtc: string;
  endedUtc: string;
  /**
   * Last frame per progress name before it ended
   */
  steps: SignalRMessage[];
  /**
   * False when the job stopped without every step sending ended
   */
  completed: boolean;
  /**
   * One per library scan that finished in this job
   */
  scanSummaries: LibraryScanSummary[];
}
