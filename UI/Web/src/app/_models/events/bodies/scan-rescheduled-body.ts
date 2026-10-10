import {ScheduledScan} from '../../activity/scheduled-scan';

export interface ScanRescheduledBody {
  /**
   * Every delayed scan after the retime, in run order
   */
  scans: ScheduledScan[];
}
