import {inject, Pipe, PipeTransform} from '@angular/core';
import {AuditSubjectType} from "../_models/kavitaplus/audit-subject-type.enum";
import {TranslocoService} from "@jsverse/transloco";

@Pipe({
  name: 'auditSubjectTitle',
})
export class AuditSubjectTitlePipe implements PipeTransform {
  private readonly translocoService = inject(TranslocoService);

  transform(value: AuditSubjectType): string {
    switch (value) {
      case AuditSubjectType.Series:
        return this.translocoService.translate('audit-subject-title-pipe.series');
      case AuditSubjectType.Person:
        return this.translocoService.translate('audit-subject-title-pipe.person');
      case AuditSubjectType.Collection:
        return this.translocoService.translate('audit-subject-title-pipe.collection');
      case AuditSubjectType.Chapter:
        return this.translocoService.translate('audit-subject-title-pipe.chapter');
      case AuditSubjectType.Global:
        return this.translocoService.translate('audit-subject-title-pipe.global');
    }
  }
}
