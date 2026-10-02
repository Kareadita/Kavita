import {ActivityRowKind} from './activity-row-kind';
import {ActivityStep} from './activity-step';
import {MessageEventPriority} from '../events/core/message-event-priority';

export interface ActivityJob {
  kind: ActivityRowKind.Job;
  id: string;
  correlationId: string | null;
  libraryId: number | null;
  priority: MessageEventPriority;
  startedUtc: string;
  updatedUtc: string;
  /**
   * Set when every step has ended, cleared if a step starts again
   */
  endedUtc: string | null;
  /**
   * By message name
   */
  steps: Record<string, ActivityStep>;
}
