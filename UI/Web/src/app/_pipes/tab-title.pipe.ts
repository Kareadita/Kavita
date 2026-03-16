import {inject, Pipe, PipeTransform} from '@angular/core';
import {Tabs} from "../_models/tabs";
import {TranslocoService} from "@jsverse/transloco";

@Pipe({
  name: 'tabTitle',
  pure: true,
  standalone: true
})
export class TabTitlePipe implements PipeTransform {
  private readonly translocoService = inject(TranslocoService);

  transform(value: Tabs): string {
    return this.translocoService.translate('tabs.' + value);
  }

}
