import {inject, Pipe, PipeTransform} from '@angular/core';
import {TranslocoService} from "@jsverse/transloco";

@Pipe({
  name: 'auditLogError'
})
export class AuditLogErrorPipe implements PipeTransform {
  private readonly translocoService = inject(TranslocoService);

  transform(key: string): string {
    if (key.includes(' ')) return key;

    const fullKey = 'audit-log-messages.' + key;
    const translated = this.translocoService.translate(fullKey);
    return translated !== fullKey ? translated : key;
  }

}
