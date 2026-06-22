export enum AuditSubjectType {
  Series = 0,
  Person = 1,
  Collection = 2,
  Chapter = 3,
  Global = 4,
}

export const allAuditSubjectTypes = [
  AuditSubjectType.Series, AuditSubjectType.Person, AuditSubjectType.Collection,
  AuditSubjectType.Chapter, AuditSubjectType.Global,
]
