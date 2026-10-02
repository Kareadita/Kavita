import {CodedEventBody} from './coded-event-body';

export interface BackupExistsBody extends CodedEventBody {
  path: string;
}
