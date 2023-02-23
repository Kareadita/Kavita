import { Pipe, PipeTransform } from '@angular/core';
import { CblBookResult } from 'src/app/_models/reading-list/cbl/cbl-book-result';
import { CblImportReason } from 'src/app/_models/reading-list/cbl/cbl-import-reason.enum';

@Pipe({
  name: 'cblConflictReason'
})
export class CblConflictReasonPipe implements PipeTransform {

  transform(result: CblBookResult): string {
    switch (result.reason) {
      case CblImportReason.AllSeriesMissing:
        return 'Your account is missing access to all series in the list or Kavita does not have anything present in the list.';
      case CblImportReason.ChapterMissing:
        return result.series + ': ' + 'Chapter ' + result.number + ' is missing from Kavita. This item will be skipped.';
      case CblImportReason.EmptyFile:
        return 'The Cbl file is empty, nothing to be done.';
      case CblImportReason.NameConflict:
        return 'A reading list already exists on your account that matches the Cbl file.';
      case CblImportReason.SeriesCollision:
        return 'The series, ' + result.series + ', collides with another series of the same name in another library.';
      case CblImportReason.SeriesMissing:
        return 'The series, ' + result.series + ', is missing from Kavita or your account does not have permission. All items with this series will be skipped from import.';
      case CblImportReason.VolumeMissing:
        return result.series + ': ' + 'Volume ' + result.volume + ' is missing from Kavita. All items with this volume number will be skipped.';
      case CblImportReason.AllChapterMissing:
        return 'All chapters cannot be matched to Chapters in Kavita.';
      case CblImportReason.Success:
        return result.series + ' volume ' + result.volume + ' number ' + result.number + ' mapped successfully';
    }
  }

}
