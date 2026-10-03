import {ActivityRowKind} from './activity-row-kind';
import {ActivityStep} from './activity-step';
import {MessageEventPriority} from '../events/core/message-event-priority';
import {ActivityEndReason} from './activity-end-reason';

export interface ActivityJob {
  kind: ActivityRowKind.Job;
  id: string;
  correlationId: string | null;
  libraryId: number | null;
  /**
   * Every library the job has sent a frame for, in order. ScanLibraries runs every library under one job
   */
  libraryIds: number[];
  priority: MessageEventPriority;
  startedUtc: string;
  updatedUtc: string;
  /**
   * Set once every step has stayed ended for a short grace, cleared if a step starts again
   */
  endedUtc: string | null;
  /**
   * Set when the snapshot, not an ended frame, closed the job
   */
  endReason: ActivityEndReason | null;
  /**
   * By message name
   */
  steps: Record<string, ActivityStep>;
  seriesAdded: number;
  seriesRemoved: number;
  /**
   * False when this client missed the start of the job (opened mid-scan or refreshed), so the series counts are partial
   */
  seenFromStart: boolean;
}
