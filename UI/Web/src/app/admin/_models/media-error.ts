import {allEnums} from "../../_helpers/enum";

export interface KavitaMediaError {
  id: number;
  /** Format Type (RAR, ZIP, 7Zip, Epub, PDF) */
  extension: string;
  /** Full Filepath to the file that has some issue */
  filePath: string;
  /** Exception message */
  details: string;
  createdUtc: string;
  libraryId?: number;
  libraryName?: string;
  seriesId?: number;
  seriesName?: string;
  /** Last time the file failed */
  lastSeenUtc: string;
  producer: MediaErrorProducer;
  reason: MediaErrorReason;
  isDismissed: boolean;
}

export enum MediaErrorProducer {
  BookService = 0,
  ArchiveService = 1,
  Scanner = 2,
}

export enum MediaErrorReason {
  Unknown = 0,
  ParseFailed = 1,
  NoSeriesName = 2,
  UnreadableArchive = 3,
  CorruptEpub = 5,
  NoPages = 6,
  CoverFailed = 7,
  WordCountFailed = 8,
  IoError = 9,
  MetadataUnreadable = 10,
  EpubNotStrict = 11,
}

export const allMediaErrorReasons = allEnums(MediaErrorReason);
