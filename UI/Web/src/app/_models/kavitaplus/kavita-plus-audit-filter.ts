import {KavitaPlusAuditCategory} from './kavita-plus-audit-category.enum';
import {AuditStatus} from './audit-status.enum';
import {AuditSubjectType} from './audit-subject-type.enum';

export interface KavitaPlusAuditFilter {
  category?: KavitaPlusAuditCategory | null;
  status?: AuditStatus | null;
  subjectType?: AuditSubjectType | null;
  userId?: number | null;
  seriesId?: number | null;
  fromUtc?: string | null;
  toUtc?: string | null;
  search?: string | null;
}
