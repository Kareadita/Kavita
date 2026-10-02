import {CodedEventBody} from './coded-event-body';

export interface ScanLibrariesDelayedBody extends CodedEventBody {
  scheduledForUtc: string;
}
