import {inject, Pipe, PipeTransform} from '@angular/core';
import {CblImportReason} from '../_models/reading-list/cbl/cbl-import-reason.enum';
import {TranslocoService} from '@jsverse/transloco';

@Pipe({
  name: 'cblImportReason',
  standalone: true
})
export class CblImportReasonPipe implements PipeTransform {

  translocoService = inject(TranslocoService);

  transform(reason: CblImportReason): string {
    switch (reason) {
      case CblImportReason.ChapterMissing:
        return this.translocoService.translate('cbl-import-reason-pipe.chapter-missing');
      case CblImportReason.VolumeMissing:
        return this.translocoService.translate('cbl-import-reason-pipe.volume-missing');
      case CblImportReason.SeriesMissing:
        return this.translocoService.translate('cbl-import-reason-pipe.series-missing');
      case CblImportReason.NameConflict:
        return this.translocoService.translate('cbl-import-reason-pipe.name-conflict');
      case CblImportReason.AllSeriesMissing:
        return this.translocoService.translate('cbl-import-reason-pipe.all-series-missing');
      case CblImportReason.EmptyFile:
        return this.translocoService.translate('cbl-import-reason-pipe.empty-file');
      case CblImportReason.SeriesCollision:
        return this.translocoService.translate('cbl-import-reason-pipe.series-collision');
      case CblImportReason.AllChapterMissing:
        return this.translocoService.translate('cbl-import-reason-pipe.all-chapter-missing');
      case CblImportReason.Success:
        return this.translocoService.translate('cbl-import-reason-pipe.success');
      case CblImportReason.InvalidFile:
        return this.translocoService.translate('cbl-import-reason-pipe.invalid-file');
      default:
        return '';
    }
  }
}
