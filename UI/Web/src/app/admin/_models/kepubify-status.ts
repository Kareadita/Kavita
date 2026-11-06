import {KepubifyCheckStatus} from './kepubify-check-status';
import {KepubifySource} from './kepubify-source';
import {KepubifyOffReason} from './kepubify-off-reason';

export interface KepubifyStatus {
  status: KepubifyCheckStatus;
  source: KepubifySource;
  path: string;
  reason: KepubifyOffReason | null;
}
