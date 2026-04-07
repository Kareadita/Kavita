import {ReadingListProvider} from '../reading-list';

export interface CblSavedFile {
  name: string;
  fileName: string;
  provider: ReadingListProvider;
  repoPath?: string;
  downloadUrl?: string;
  sha?: string;
}
