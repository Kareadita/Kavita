import {inject, Pipe, PipeTransform} from '@angular/core';
import {TranslocoService} from "@jsverse/transloco";
import {CblImportResult} from "../_models/reading-list/cbl/cbl-import-result.enum";

@Pipe({
  name: 'cblImportResult',
  standalone: true
})
export class CblImportResultPipe implements PipeTransform {

  translocoService = inject(TranslocoService);

  transform(result: CblImportResult): string {
    switch (result) {
      case CblImportResult.Success:
        return this.translocoService.translate('cbl-import-result-pipe.success');
      case CblImportResult.Partial:
        return this.translocoService.translate('cbl-import-result-pipe.partial');
      case CblImportResult.Fail:
        return this.translocoService.translate('cbl-import-result-pipe.failure');
    }
  }
}
