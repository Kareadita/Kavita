import {KavitaPlusAuditCategory} from './kavita-plus-audit-category.enum';
import {KavitaPlusEventType} from './kavita-plus-event-type.enum';
import {AuditStatus} from './audit-status.enum';
import {AuditSubjectType} from './audit-subject-type.enum';
import {KavitaPlusScrobbleDetails} from "./kavita-plus-scrobble-details";
import {MetadataFieldChange} from "./metadata-field-change";


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
  userId: number | null;
  username: string | null;
  diff: MetadataFieldChange[] | null;
  errorMessage: string | null;
  scrobbleDetails: KavitaPlusScrobbleDetails | null;
  canRetry: boolean;
}
