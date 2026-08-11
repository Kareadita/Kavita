import {inject, Pipe, PipeTransform} from '@angular/core';
import {TranslocoService} from "@jsverse/transloco";
import {TagWeight} from "../admin/_models/tag-weight.enum";

@Pipe({
  name: 'tagWeightTitle',
})
export class TagWeightTitlePipe implements PipeTransform {
  private readonly translocoService = inject(TranslocoService);
  transform(value: TagWeight): string {
    switch (value) {
      case TagWeight.Core:
        return this.t('core');
      case TagWeight.Defining:
        return this.t('defining');
      case TagWeight.Recurrent:
        return this.t('recurrent');
      case TagWeight.Incidental:
        return this.t('incidental');
      case TagWeight.Unweighted:
        return this.t('unweighted');
    }
    return value;
  }

  t(s: string) {
    return this.translocoService.translate(`tag-weight-title-pipe.${s}`);
  }

}
