import {Volume} from "../../_models/volume";
import {Chapter} from "../../_models/chapter";

export type DownloadQueueStatus = 'queued' | 'preparing' | 'downloading' | 'completed' | 'failed' | 'cancelled';

export interface DownloadQueueItem {
  id: number;
  /** Atomic unit of download, series/reading-list/collection always decompose to these */
  entityType: 'volume' | 'chapter';
  entityId: number;
  libraryId: number;
  seriesId: number;
  /** Human-readable label, e.g. "My Series - Vol. 3" */
  label: string;
  /** Volume or chapter number string */
  subLabel: string;
  seriesName: string;
  /** Bytes, 0 if unknown */
  estimatedSize: number;
  status: DownloadQueueStatus;
  /** 0-100 */
  progress: number;
  errorMessage: string;
  retryCount: number;
  /** UTC ISO string when the item was queued */
  queuedAt: string | number;
  /** Present only for in-memory items; stripped before IndexedDB persistence and absent on restored items. */
  entity?: Volume | Chapter;
  /** Predicted backend filename used to match SignalR progress events */
  downloadName: string;
  /** Smoothed bytes/sec, in-memory only */
  speedBps?: number;
  /** Estimated seconds remaining, in-memory only */
  etaSeconds?: number;
  /** UTC ISO string when completed or failed */
  completedAt?: string | number;
}
