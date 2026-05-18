export enum AuditStatus {
  Success = 0,
  Failure = 1,
  Info = 2,
}

export const allAuditStatuses: AuditStatus[] = [
  AuditStatus.Success, AuditStatus.Info, AuditStatus.Failure
]
