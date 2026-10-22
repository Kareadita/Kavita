export enum FolderWatchMode {
  Off = 0,
  Automatic = 1,
  FileWatcher = 2,
  Polling = 3,
}

export const allFolderWatchModes = [FolderWatchMode.Automatic, FolderWatchMode.FileWatcher, FolderWatchMode.Polling, FolderWatchMode.Off];
