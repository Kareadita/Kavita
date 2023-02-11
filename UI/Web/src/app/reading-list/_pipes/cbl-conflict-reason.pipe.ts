import { Pipe, PipeTransform } from '@angular/core';
import { CblImportReason } from 'src/app/_models/reading-list/cbl/cbl-import-reason.enum';

@Pipe({
  name: 'cblConflictReason'
})
export class CblConflictReasonPipe implements PipeTransform {

  transform(reason: CblImportReason, seriesName: string, volumeNumber: string = "", chapterNumber: string = ""): unknown {
    switch (reason) {
      case CblImportReason.AllSeriesMissing:
        return 'Your account is missing access to all series in the list or Kavita does not have anything present in the list.';
      case CblImportReason.ChapterMissing:
        return 'Chapter ' + chapterNumber + ' is missing from Kavita. This item will be skipped.';
      case CblImportReason.EmptyFile:
        return 'The Cbl file is empty, nothing to be done.';
      case CblImportReason.NameConflict:
        return 'A reading list already exists on your account that matches the Cbl file.';
      case CblImportReason.SeriesCollision:
        return 'The series, ' + seriesName + ', collides with another series of the same name in another library.';
      case CblImportReason.SeriesMissing:
        return 'The series, ' + seriesName + ', is missing from Kavita or your account does not have permission. All items with this series will be skipped from import.';
      case CblImportReason.VolumeMissing:
        return 'Volume ' + volumeNumber + ' is missing from Kavita. All items with this volume number will be skipped.';
    }
  }

}
