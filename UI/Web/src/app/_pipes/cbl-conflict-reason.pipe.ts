import {inject, Pipe, PipeTransform} from '@angular/core';
import { CblBookResult } from 'src/app/_models/reading-list/cbl/cbl-book-result';
import { CblImportReason } from 'src/app/_models/reading-list/cbl/cbl-import-reason.enum';
import {TranslocoService} from "@jsverse/transloco";

const failIcon = '<i aria-hidden="true" class="reading-list-fail--item fa-solid fa-circle-xmark me-1"></i>';
const successIcon = '<i aria-hidden="true" class="reading-list-success--item fa-solid fa-circle-check me-1"></i>';

@Pipe({
  name: 'cblConflictReason',
  standalone: true
})
export class CblConflictReasonPipe implements PipeTransform {

  translocoService = inject(TranslocoService);

  transform(result: CblBookResult): string {
    switch (result.reason) {
      case CblImportReason.AllSeriesMissing:
        return failIcon + this.translocoService.translate('cbl-conflict-reason-pipe.all-series-missing');
      case CblImportReason.ChapterMissing:
        return failIcon + this.translocoService.translate('cbl-conflict-reason-pipe.chapter-missing', {series: result.series, chapter: result.number});
      case CblImportReason.EmptyFile:
        return failIcon + this.translocoService.translate('cbl-conflict-reason-pipe.empty-file');
      case CblImportReason.NameConflict:
        return failIcon + this.translocoService.translate('cbl-conflict-reason-pipe.name-conflict', {readingListName: result.readingListName});
      case CblImportReason.SeriesCollision:
        return failIcon + this.translocoService.translate('cbl-conflict-reason-pipe.series-collision', {seriesLink: `<a href="/library/${result.libraryId}/series/${result.seriesId}" target="_blank">${result.series}</a>`});
      case CblImportReason.SeriesMissing:
        return failIcon + this.translocoService.translate('cbl-conflict-reason-pipe.series-missing', {series: result.series});
      case CblImportReason.VolumeMissing:
        return failIcon + this.translocoService.translate('cbl-conflict-reason-pipe.volume-missing', {series: result.series, volume: result.volume});
      case CblImportReason.AllChapterMissing:
        return failIcon + this.translocoService.translate('cbl-conflict-reason-pipe.all-chapter-missing');
      case CblImportReason.Success:
        return successIcon + this.translocoService.translate('cbl-conflict-reason-pipe.volume-missing', {series: result.series, volume: result.volume, chapter: result.number});
      case CblImportReason.InvalidFile:
        return failIcon + this.translocoService.translate('cbl-conflict-reason-pipe.invalid-file');
    }
  }

}
