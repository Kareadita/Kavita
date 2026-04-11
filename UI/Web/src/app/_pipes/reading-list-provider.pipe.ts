import {inject, Pipe, PipeTransform} from '@angular/core';
import {ReadingListProvider} from "../_models/reading-list/reading-list";
import {TranslocoService} from "@jsverse/transloco";

@Pipe({
  name: 'readingListProvider',
  standalone: true,
  pure: true
})
export class ReadingListProviderPipe implements PipeTransform {

  private readonly translocoService = inject(TranslocoService);

  transform(value: ReadingListProvider): string {
    switch (value) {
      case ReadingListProvider.None:
        return this.translocoService.translate('reading-list-provider-pipe.none');
      case ReadingListProvider.File:
        return this.translocoService.translate('reading-list-provider-pipe.file');
      case ReadingListProvider.Url:
        return this.translocoService.translate('reading-list-provider-pipe.url');

    }
  }

}
