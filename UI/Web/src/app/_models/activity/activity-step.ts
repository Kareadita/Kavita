import {MessageEventCode} from '../events/core/message-event-code';
import {SignalRMessage} from '../events/core/signalr-message';

export interface ActivityStep {
  name: string;
  code: MessageEventCode | null;
  eventType: SignalRMessage['eventType'];
  progressType: SignalRMessage['progress'];
  title: string;
  subTitle: string;
  /**
   * 0 to 1, never lower than an earlier value with the same code. Null when indeterminate
   */
  progress: number | null;
  body: unknown;
  updatedUtc: string;
}
