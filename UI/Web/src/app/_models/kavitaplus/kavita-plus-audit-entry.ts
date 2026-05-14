import {KavitaPlusAuditCategory} from './kavita-plus-audit-category.enum';
import {KavitaPlusEventType} from './kavita-plus-event-type.enum';
import {AuditStatus} from './audit-status.enum';
import {AuditSubjectType} from './audit-subject-type.enum';

export interface MetadataFieldChange {
  field: string;
  from: unknown;
  to: unknown;
}

export interface KavitaPlusAuditEntry {
  id: number;
  createdUtc: string;
  category: KavitaPlusAuditCategory;
  eventType: KavitaPlusEventType;
  status: AuditStatus;
  seriesId: number | null;
  seriesName: string | null;
  subjectType: AuditSubjectType;
  subjectId: number | null;
  subjectName: string | null;
  userId: number | null;
  username: string | null;
  diff: MetadataFieldChange[] | null;
  errorMessage: string | null;
}
