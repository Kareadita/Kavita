import { Pipe, PipeTransform } from '@angular/core';
import {FileTypeGroup} from "../_models/library/file-type-group.enum";
import {translate} from "@jsverse/transloco";

@Pipe({
  name: 'fileTypeGroup',
  standalone: true
})
export class FileTypeGroupPipe implements PipeTransform {

  transform(value: FileTypeGroup): string {
    switch (value) {
      case FileTypeGroup.Archive:
        return translate('file-type-group-pipe.archive');
      case FileTypeGroup.Epub:
        return translate('file-type-group-pipe.epub');
      case FileTypeGroup.Pdf:
        return translate('file-type-group-pipe.pdf');
      case FileTypeGroup.Images:
        return translate('file-type-group-pipe.image');

    }
  }

}
