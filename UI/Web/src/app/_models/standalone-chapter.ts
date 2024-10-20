import {Chapter} from "./chapter";
import {LibraryType} from "./library/library";

export interface StandaloneChapter extends Chapter {
  seriesId: number;
  libraryId: number;
  libraryType: LibraryType;
  volumeTitle?: string;
}
