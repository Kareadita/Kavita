import {KavitaPlusAuditCategory} from './kavita-plus-audit-category.enum';
import {KavitaPlusEventType} from './kavita-plus-event-type.enum';
import {AuditStatus} from './audit-status.enum';
import {AuditSubjectType} from './audit-subject-type.enum';
import {ScrobbleEventType} from '../scrobbling/scrobble-event';
import {ScrobbleProvider} from "../../_services/scrobbling.service";
import {LibraryType} from '../library/library';

export interface MetadataFieldChange {
  field: string;
  from: unknown;
  to: unknown;
}

export interface KavitaPlusScrobbleDetails {
  scrobbleEventType: ScrobbleEventType | null;
  chapterNumber: number | null;
  volumeNumber: number | null;
  rating: number | null;
  provider: ScrobbleProvider;
  libraryType: LibraryType;
}

export interface KavitaPlusAuditEntry {
  id: number;
  createdUtc: string;
  category: KavitaPlusAuditCategory;
  eventType: KavitaPlusEventType;
  status: AuditStatus;
  seriesId: number | null;
  libraryId: number | null;
  seriesName: string | null;
  subjectType: AuditSubjectType;
  subjectId: number | null;
  subjectName: string | null;
  userId: number | null;
  username: string | null;
  diff: MetadataFieldChange[] | null;
  errorMessage: string | null;
  scrobbleDetails: KavitaPlusScrobbleDetails | null;
  canRetry: boolean;
}
