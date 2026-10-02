import {CodedEventBody} from './coded-event-body';

export interface BackupFolderUnwritableBody extends CodedEventBody {
  folder: string;
}
