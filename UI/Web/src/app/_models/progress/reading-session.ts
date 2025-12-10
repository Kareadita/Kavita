import {ClientInfo} from "../../_services/client-info.service";

export interface ReadingSession {
  id: number;
  startTimeUtc: string;
  endTimeUtc: string;
  isActive: boolean;
  activityData: ReadingActivityData[];
  userId: number;
  username: string;
}

export interface ReadingActivityData {
  chapterId: number;
  volumeId: number;
  seriesId: number;
  libraryId: number;

  seriesName: string;
  libraryName: string;
  chapterTitle: string;
  wordsRead: number;
  pagesRead: number;
  startPage: number;
  endPage: number;
  totalPages: number;
  totalWords: number;

  startTimeUtc: string;
  endTimeUtc: string;

  clientInfo: ClientInfo | null;
}

export enum AuthenticationType
{
  Unknown = 0,
  JWT = 1,
  AuthKey = 2,
  OIDC = 3
}
