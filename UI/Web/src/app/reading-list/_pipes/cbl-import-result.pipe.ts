import { Pipe, PipeTransform } from '@angular/core';
import { CblImportResult } from 'src/app/_models/reading-list/cbl/cbl-import-result.enum';

@Pipe({
  name: 'cblImportResult',
  standalone: true
})
export class CblImportResultPipe implements PipeTransform {

  transform(result: CblImportResult): string {
    switch (result) {
      case CblImportResult.Success:
        return 'Success';
      case CblImportResult.Partial:
        return 'Partial';
      case CblImportResult.Fail:
        return 'Failure';
    }
  }
}
