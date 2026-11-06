import {CodedEventBody} from './coded-event-body';
import {KepubifyStatus} from '../../../admin/_models/kepubify-status';

export interface KepubifyCheckedBody extends CodedEventBody {
  status: KepubifyStatus;
}
