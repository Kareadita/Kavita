import {MessageEventPriority} from './message-event-priority';
import {MessageEventCode} from './message-event-code';

export interface SignalRMessage<T = unknown> {
  body: T;
  name: string;
  title: string;
  subTitle: string;
  eventType: 'single' | 'started' | 'updated' | 'ended';
  /**
   * The server sends '' for none
   */
  progress: '' | 'indeterminate' | 'determinate';
  eventTimeUtc: string;
  priority: MessageEventPriority | null;
  code: MessageEventCode | null;
  correlationId: string | null;
}
