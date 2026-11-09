import {MangaFormat} from "../../_models/manga-format";
import {PublicationStatus} from "../../_models/metadata/publication-status";

export interface BookInfo {
  bookTitle: string;
  seriesFormat: MangaFormat;
  seriesId: number;
  seriesName: string;
  seriesPublicationStatus: PublicationStatus | null;
  libraryId: number;
  volumeId: number;
}
