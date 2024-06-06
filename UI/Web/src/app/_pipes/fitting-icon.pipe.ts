import { Pipe, PipeTransform } from '@angular/core';
import { FITTING_OPTION } from '../manga-reader/_models/reader-enums';

@Pipe({
  name: 'fittingIcon',
  pure: true,
  standalone: true,
})
export class FittingIconPipe implements PipeTransform {

  transform(fit: FITTING_OPTION): string {
    switch(fit) {
      case FITTING_OPTION.HEIGHT:
        return 'fa fa-arrows-alt-v';
      case FITTING_OPTION.WIDTH:
        return 'fa fa-arrows-alt-h';
      case FITTING_OPTION.ORIGINAL:
        return 'fa fa-expand-arrows-alt';
    }
  }

}
