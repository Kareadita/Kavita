export interface CblRepoItem {
  name: string;
  path: string;
  isDirectory: boolean;
  sha: string;
  size: number;
  downloadUrl: string | null;
  existingReadingListId: number | null;
  alreadySynced: boolean;
}
