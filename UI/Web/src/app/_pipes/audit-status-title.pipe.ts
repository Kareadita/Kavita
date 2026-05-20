import {inject, Pipe, PipeTransform} from '@angular/core';
import {AuditStatus} from "../_models/kavitaplus/audit-status.enum";
import {TranslocoService} from "@jsverse/transloco";

@Pipe({
  name: 'auditStatusTitle',
})
export class AuditStatusTitlePipe implements PipeTransform {
  private readonly translocoService = inject(TranslocoService);

  transform(value: AuditStatus): string {
    switch (value) {
      case AuditStatus.Success:
        return this.translocoService.translate('audit-status-title-pipe.success');
      case AuditStatus.Failure:
        return this.translocoService.translate('audit-status-title-pipe.failure');
      case AuditStatus.Info:
        return this.translocoService.translate('audit-status-title-pipe.info');
    }
  }
}
