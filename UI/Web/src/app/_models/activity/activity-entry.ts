import {ActivityRowKind} from './activity-row-kind';
import {MessageEventCode} from '../events/core/message-event-code';
import {MessageEventPriority} from '../events/core/message-event-priority';

export interface ActivityEntry {
  kind: ActivityRowKind.Entry;
  id: string;
  name: string;
  code: MessageEventCode | null;
  priority: MessageEventPriority;
  title: string;
  subTitle: string;
  body: unknown;
  correlationId: string | null;
  libraryId: number | null;
  seriesId: number | null;
  scheduledForUtc: string | null;
  updatedUtc: string;
}
