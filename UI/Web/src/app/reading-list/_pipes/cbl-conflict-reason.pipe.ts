import { Pipe, PipeTransform } from '@angular/core';
import { CblBookResult } from 'src/app/_models/reading-list/cbl/cbl-book-result';
import { CblImportReason } from 'src/app/_models/reading-list/cbl/cbl-import-reason.enum';
import { LibraryService } from 'src/app/_services/library.service';

const failIcon = '<i aria-hidden="true" class="reading-list-fail--item fa-solid fa-circle-xmark me-1"></i>';
const successIcon = '<i aria-hidden="true" class="reading-list-success--item fa-solid fa-circle-check me-1"></i>';

@Pipe({
  name: 'cblConflictReason'
})
export class CblConflictReasonPipe implements PipeTransform {

  constructor(private libraryService: LibraryService) {}

  transform(result: CblBookResult): string {
    switch (result.reason) {
      case CblImportReason.AllSeriesMissing:
        return failIcon + 'Your account is missing access to all series in the list or Kavita does not have anything present in the list.';
      case CblImportReason.ChapterMissing:
        return failIcon + result.series + ': ' + 'Chapter ' + result.number + ' is missing from Kavita. This item will be skipped.';
      case CblImportReason.EmptyFile:
        return failIcon + 'The cbl file is empty, nothing to be done.';
      case CblImportReason.NameConflict:
        return failIcon + 'A reading list already exists on your account that matches the cbl file.';
      case CblImportReason.SeriesCollision:
        return failIcon + 'The series, ' + `<a href="/library/${result.libraryId}/series/${result.seriesId}" target="_blank">${result.series}</a>` + ', collides with another series of the same name in another library.';
      case CblImportReason.SeriesMissing:
        return failIcon + 'The series, ' + result.series + ', is missing from Kavita or your account does not have permission. All items with this series will be skipped from import.';
      case CblImportReason.VolumeMissing:
        return failIcon + result.series + ': ' + 'Volume ' + result.volume + ' is missing from Kavita. All items with this volume number will be skipped.';
      case CblImportReason.AllChapterMissing:
        return failIcon + 'All chapters cannot be matched to Chapters in Kavita.';
      case CblImportReason.Success:
        return successIcon + result.series + ' volume ' + result.volume + ' number ' + result.number + ' mapped successfully.';
      case CblImportReason.InvalidFile:
        return failIcon + 'The file is corrupted or not matching the expected tags/spec.';
    }
  }

}
