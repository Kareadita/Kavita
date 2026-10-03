import {ActivitySnapshot} from './activity-snapshot';

export interface ActivitySnapshotResult {
  snapshot: ActivitySnapshot;
  /**
   * Client clock when the request went out. A job with a frame after this is newer than the snapshot
   */
  requestedAtMs: number;
}
