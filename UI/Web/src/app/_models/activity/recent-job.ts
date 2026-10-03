import {SignalRMessage} from '../events/core/signalr-message';

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
  seriesAdded: number;
  seriesRemoved: number;
}
