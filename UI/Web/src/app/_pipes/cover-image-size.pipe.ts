import {Pipe, PipeTransform} from '@angular/core';
import {CoverImageSize} from "../admin/_models/cover-image-size";
import {translate} from "@jsverse/transloco";

@Pipe({
  name: 'coverImageSize',
  standalone: true
})
export class CoverImageSizePipe implements PipeTransform {

  transform(value: CoverImageSize): string {
    switch (value) {
      case CoverImageSize.Default:
        return translate('cover-image-size.default');
      case CoverImageSize.Medium:
        return translate('cover-image-size.medium');
      case CoverImageSize.Large:
        return translate('cover-image-size.large');
      case CoverImageSize.XLarge:
        return translate('cover-image-size.xlarge');

    }
  }

}
