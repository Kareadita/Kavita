import {Volume} from "../../_models/volume";
import {Chapter} from "../../_models/chapter";

export type DownloadQueueStatus = 'queued' | 'preparing' | 'downloading' | 'completed' | 'failed' | 'cancelled';

export interface DownloadQueueItem {
  id: number;
  /** Atomic unit of download — series/reading-list/collection always decompose to these */
  entityType: 'volume' | 'chapter';
  entityId: number;
  /** Human-readable label, e.g. "My Series - Vol. 3" */
  label: string;
  /** Volume or chapter number string */
  subLabel: string;
  seriesName: string;
  /** Bytes, 0 if unknown */
  estimatedSize: number;
  status: DownloadQueueStatus;
  /** 0-100, driven by SignalR DownloadProgress events */
  progress: number;
  errorMessage: string;
  retryCount: number;
  /** Date.now() timestamp when the item was queued */
  queuedAt: number;
  entity: Volume | Chapter;
  /** Predicted backend filename used to match SignalR progress events */
  downloadName: string;
}
