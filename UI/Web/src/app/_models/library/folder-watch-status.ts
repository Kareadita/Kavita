import {FolderWatchMode} from './folder-watch-mode';
import {FolderWatchMethod} from './folder-watch-method';
import {FolderWatchFallbackReason} from '../events/core/folder-watch-fallback-reason';

export interface FolderWatchStatus {
  mode: FolderWatchMode;
  method: FolderWatchMethod;
  reason: FolderWatchFallbackReason | null;
  folder: string | null;
  /**
   * Lowest among the library's mounts in Trial
   */
  successes: number;
  successesNeeded: number;
  needsLibraryScan: boolean;
  enableFolderWatching: boolean;
}
